using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using ImGuiNET;

namespace SimpleTweaksPlugin {
    public class SetOptionCommand : Tweak {

        public override string Name => "Set Option Command";

        private IntPtr setOptionAddress = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
        private delegate IntPtr SetOptionDelegate(IntPtr baseAddress, ulong kind, ulong value, ulong unknown);
        
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)] 
        private delegate void SetGamepadMode(IntPtr baseAddress, ulong value);

        private SetOptionDelegate setOption;
        private SetGamepadMode setGamepadMode;

        private Hook<SetOptionDelegate> setOptionHook;
        
        private IntPtr baseAddress = IntPtr.Zero;

        private enum OptionType {
            Bool,
            ToggleGamepadMode, // bool with extra shit
        }

        private readonly Dictionary<string, (OptionType type, ulong key, int offset, string[] alias)> optionKinds = new Dictionary<string, (OptionType, ulong, int, string[])> {
            { "itemtooltips", (OptionType.Bool, 0x130, 0xBDE0, new [] {"itt"} )},
            { "actiontooltips", (OptionType.Bool, 0x136, 0xBE40, new [] {"att"}) },
            { "gamepadmode", (OptionType.ToggleGamepadMode, 0x89, 0xB370, new [] { "gp" })},
            { "legacymovement", (OptionType.Bool, 0x8A, 0xB380, new [] { "lm"})},
        };

        private readonly Dictionary<OptionType, string> optionTypeValueHints = new Dictionary<OptionType, string> {
            {OptionType.Bool, "on | off | toggle"},
            {OptionType.ToggleGamepadMode, "on | off | toggle"},
        };
        
        public override void Setup() {
            if (Ready) return;

            try {
                if (setOptionAddress == IntPtr.Zero) {
                    setOptionAddress = PluginInterface.TargetModuleScanner.ScanText("89 54 24 10 53 55 57 41 54 41 55 41 56 48 83 EC 48 8B C2 45 8B E0 44 8B D2 45 32 F6 44 8B C2 45 32 ED");
                    PluginLog.Log(setOptionAddress.ToInt64().ToString("X"));
                    setOption = Marshal.GetDelegateForFunctionPointer<SetOptionDelegate>(setOptionAddress);
                }

                var toggleGamepadModeAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 40 0F B6 DF 49 8B CC");
                PluginLog.Log(toggleGamepadModeAddress.ToInt64().ToString("X"));
                setGamepadMode = Marshal.GetDelegateForFunctionPointer<SetGamepadMode>(toggleGamepadModeAddress);
                
                if (setOptionAddress == IntPtr.Zero) {
                    PluginLog.LogError($"Failed to setup {GetType().Name}: Failed to find required functions.");
                    return;
                }

                Ready = true;

            } catch (Exception ex) {
                PluginLog.LogError($"Failed to setup {this.GetType().Name}: {ex.Message}");
            }
        }

        public override bool DrawConfig() {
            if (!Enabled) return base.DrawConfig();
            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {
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

                ImGui.TreePop();
            }

            return false;
        }

        public override void Enable() {
            if (!Ready) return;
            setOptionHook ??= new Hook<SetOptionDelegate>(setOptionAddress, new SetOptionDelegate(SetOptionDetour));
            setOptionHook?.Enable();

            PluginInterface.CommandManager.AddHandler("/setoption", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = true});
            PluginInterface.CommandManager.AddHandler("/setopt", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = false});

            Enabled = true;
        }

        private void OptionCommand(string command, string arguments) {

            if (baseAddress == IntPtr.Zero) {
                PluginInterface.Framework.Gui.Chat.PrintError("Waiting for setup. Please switch zone.");
                return;
            }

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

            (OptionType type, ulong key, int offset, string[] alias) optionDefinition;
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
                            setOption(baseAddress, optionDefinition.key, 1, 2);
                            setValue = 1;
                            break;
                        case "0":
                        case "false":
                        case "off":
                            setOption(baseAddress, optionDefinition.key, 0, 2);
                            setValue = 0;
                            break;
                        case "":
                        case "t":
                        case "toggle":
                            if (optionDefinition.offset > 0) {
                                var cVal = Marshal.ReadByte(baseAddress, optionDefinition.offset);
                                setOption(baseAddress, optionDefinition.key, cVal == 1 ? 0UL : 1UL, 2);
                                setValue = cVal == 1 ? 1 : 0UL;
                            } else {
                                PluginInterface.Framework.Gui.Chat.PrintError($"Toggle not available for {optionKind}");
                            }
                            break;
                        default:
                            PluginInterface.Framework.Gui.Chat.PrintError($"/setoption {optionKind} ({optionTypeValueHints[optionDefinition.type]})");
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
                    setGamepadMode(baseAddress, setValue);
                    break;
                }
            }
            
        }

        private IntPtr SetOptionDetour(IntPtr baseAddress, ulong kind, ulong value, ulong unknown) {
            this.baseAddress = baseAddress;
#if DEBUG
            PluginLog.Log($"Set Option: {baseAddress.ToInt64():X} {kind:X}, {value:X}, {unknown:X}");
#endif
            return setOptionHook.Original(baseAddress, kind, value, unknown);
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
