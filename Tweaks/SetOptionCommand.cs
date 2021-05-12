using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using FFXIVClientInterface.Client.UI.Misc;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class SetOptionCommand : Tweak {

        public override string Name => "Set Option Command";
        public override string Description => "Adds commands to change various settings.";

        private IntPtr setOptionAddress = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
        private delegate IntPtr SetOptionDelegate(ConfigModuleStruct* configModule, ulong kind, ulong value, ulong unknown);
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
        private delegate void SetGamepadMode(ConfigModuleStruct* configModule, ulong value);

        private SetOptionDelegate setOption;
        private SetGamepadMode setGamepadMode;

        private Hook<SetOptionDelegate> setOptionHook;
        
        private enum OptionType {
            Bool,
            ToggleGamepadMode, // bool with extra shit
            NameDisplayModeBattle,
            NameDisplayMode,
        }

        private readonly Dictionary<string, (OptionType type, ulong key, string[] alias)> optionKinds = new() {
            { "itemtooltips", (OptionType.Bool, 0x132, new [] {"itt"} )},
            { "actiontooltips", (OptionType.Bool, 0x138, new [] {"att"}) },
            { "gamepadmode", (OptionType.ToggleGamepadMode, 0x8B, new [] { "gp" })},
            { "legacymovement", (OptionType.Bool, 0x8C, new [] { "lm"})},
            { "owndisplayname", (OptionType.NameDisplayModeBattle, 0x172, new [] { "odn" })},
            { "partydisplayname", (OptionType.NameDisplayModeBattle, 0x17E, new [] { "pdn" })},
            { "alliancedisplayname", (OptionType.NameDisplayModeBattle, 0x187, new [] {"adn"})},
            { "otherpcdisplayname", (OptionType.NameDisplayModeBattle, 0x18E, new [] {"opcdn"})},
            { "frienddisplayname", (OptionType.NameDisplayModeBattle, 0x1CB, new [] {"fdn"})},
        };

        private readonly Dictionary<OptionType, string> optionTypeValueHints = new Dictionary<OptionType, string> {
            {OptionType.Bool, "on | off | toggle"},
            {OptionType.ToggleGamepadMode, "on | off | toggle"},
            {OptionType.NameDisplayModeBattle, "always | battle | targeted | never"},
        };
        
        public override void Setup() {
            if (Ready) return;

            try {
                if (setOptionAddress == IntPtr.Zero) {
                    setOptionAddress = PluginInterface.TargetModuleScanner.ScanText("89 54 24 10 53 55 57 41 54 41 55 41 56 48 83 EC 48 8B C2 45 8B E0 44 8B D2 45 32 F6 44 8B C2 45 32 ED");
                    SimpleLog.Verbose($"SetOptionAddress: {setOptionAddress.ToInt64():X}");
                    setOption = Marshal.GetDelegateForFunctionPointer<SetOptionDelegate>(setOptionAddress);
                }

                var toggleGamepadModeAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 40 0F B6 DF 49 8B CC");
                SimpleLog.Verbose($"ToggleGamePadModeAddress: {toggleGamepadModeAddress.ToInt64():X}");
                setGamepadMode = Marshal.GetDelegateForFunctionPointer<SetGamepadMode>(toggleGamepadModeAddress);
                
                if (setOptionAddress == IntPtr.Zero) {
                    SimpleLog.Error($"Failed to setup {GetType().Name}: Failed to find required functions.");
                    return;
                }

                Ready = true;

            } catch (Exception ex) {
                SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
            }
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            ImGui.TextDisabled("/setopt list");
            ImGui.TextDisabled("/setopt [option] [value]");

            if (ImGui.TreeNode("Available Options##optionListTree")) {
                    
                ImGui.Columns(3);
                ImGui.Text("option");
                ImGui.NextColumn();
                ImGui.Text("values");
                ImGui.NextColumn();
                ImGui.Text("alias");
                ImGui.Separator();

                foreach (var o in optionKinds) {
                    if (o.Value.type == OptionType.ToggleGamepadMode) continue;
                    ImGui.NextColumn();
                    ImGui.Text(o.Key);
                    ImGui.NextColumn();
                    ImGui.Text(optionTypeValueHints.ContainsKey(o.Value.type) ? optionTypeValueHints[o.Value.type] : "");
                    ImGui.NextColumn();
                    var sb = new StringBuilder();
                    foreach (var a in o.Value.alias) {
                        sb.Append(a);
                        sb.Append(" ");
                    }
                    ImGui.Text(sb.ToString());
                }


                ImGui.Columns();
                ImGui.TreePop();
            }
        };

        public override void Enable() {
            if (!Ready) return;
            setOptionHook ??= new Hook<SetOptionDelegate>(setOptionAddress, new SetOptionDelegate(SetOptionDetour));
            setOptionHook?.Enable();

            PluginInterface.CommandManager.AddHandler("/setoption", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = true});
            PluginInterface.CommandManager.AddHandler("/setopt", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = false});

            Enabled = true;
        }

        private void OptionCommand(string command, string arguments) {
            var configModule = SimpleTweaksPlugin.Client.UiModule.ConfigModule;
            if (configModule == null) return;

            var argList = arguments.ToLower().Split(' ');

            if (argList[0] == "list") {

                var sb = new StringBuilder();

                foreach (var o in optionKinds.Keys) {
                    if (optionKinds[o].type == OptionType.ToggleGamepadMode) continue;
                    sb.Append(o + " ");
                }
                PluginInterface.Framework.Gui.Chat.Print($"Options:\n{sb}");

                return;
            }
            
            var optionKind = argList[0];

            (OptionType type, ulong key, string[] alias) optionDefinition;
            if (optionKinds.ContainsKey(optionKind)) {
                optionDefinition = optionKinds[optionKind];
            } else {
                var fromAlias = optionKinds.Values.Where(ok => ok.alias.Contains(optionKind)).ToArray();

                if (fromAlias.Length == 0) {
                    PluginInterface.Framework.Gui.Chat.PrintError("Unknown Option");
                    PluginInterface.Framework.Gui.Chat.PrintError("/setoption list for a list of options");
                    return;
                } 
                optionDefinition = fromAlias[0];
            }

            var optionValue = "";
            if (argList.Length >= 2) {
                optionValue = argList[1];
            }

            var setValue = 0UL;
            switch (optionDefinition.type) {
                
                case OptionType.ToggleGamepadMode:
                case OptionType.Bool: {

                    switch (optionValue) {
                        case "1":
                        case "true":
                        case "on":
                            setOption(configModule, optionDefinition.key, 1, 2);
                            setValue = 1;
                            break;
                        case "0":
                        case "false":
                        case "off":
                            setOption(configModule, optionDefinition.key, 0, 2);
                            setValue = 0;
                            break;
                        case "":
                        case "t":
                        case "toggle":
                            var cVal = configModule.GetOptionBoolean(optionDefinition.key);
                            setOption(configModule, optionDefinition.key, cVal ? 0UL : 1UL, 2);
                            setValue = cVal ? 1 : 0UL;
                            break;
                        default:
                            PluginInterface.Framework.Gui.Chat.PrintError($"/setoption {optionKind} ({optionTypeValueHints[optionDefinition.type]})");
                            break;
                        }

                    break;
                }
                case OptionType.NameDisplayModeBattle: {
                    switch (optionValue.ToLowerInvariant()) {
                        case "a":
                        case "always":
                            setOption(configModule, optionDefinition.key, 0, 2);
                            break;
                        case "b":
                        case "battle":
                            setOption(configModule, optionDefinition.key, 1, 2);
                            break;
                        case "t":
                        case "target":
                        case "targeted":
                            setOption(configModule, optionDefinition.key, 2, 2);
                            break;
                        case "n":
                        case "never": 
                            setOption(configModule, optionDefinition.key, 3, 2);
                            break;
                    }
                    break;
                }
                default:
                    PluginInterface.Framework.Gui.Chat.PrintError("Unsupported Option");
                    return;
            }

            switch (optionDefinition.type) {
                case OptionType.ToggleGamepadMode: {
                    setGamepadMode(configModule, setValue);
                    break;
                }
            }
            
        }

        private IntPtr SetOptionDetour(ConfigModuleStruct* configModule, ulong kind, ulong value, ulong unknown) {
            SimpleLog.Verbose($"Set Option: {(ulong)configModule:X} {kind:X}, {value:X}, {unknown:X}");
            return setOptionHook.Original(configModule, kind, value, unknown);
        }

        public override void Disable() {
            setOptionHook?.Disable();
            PluginInterface.CommandManager.RemoveHandler("/setoption");
            PluginInterface.CommandManager.RemoveHandler("/setopt");
            Enabled = false;
        }

        public override void Dispose() {
            setOptionHook?.Dispose();
            Enabled = false;
            Ready = false;
        }
    }
}
