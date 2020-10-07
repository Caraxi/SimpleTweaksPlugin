using System;
using System.Collections.Generic;
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

        private SetOptionDelegate setOption;

        private Hook<SetOptionDelegate> setOptionHook;
        
        private IntPtr baseAddress = IntPtr.Zero;

        private enum OptionType {
            Bool,
        }

        private readonly Dictionary<string, (OptionType type, ulong key, int offset)> optionKinds = new Dictionary<string, (OptionType, ulong, int)> {
            { "itemtooltips", (OptionType.Bool, 0x130, 0xBDE0)},
            { "actiontooltips", (OptionType.Bool, 0x136, 0xBE40) },
        };


        public override void Setup() {
            if (Ready) return;

            try {
                if (setOptionAddress == IntPtr.Zero) {
                    setOptionAddress = PluginInterface.TargetModuleScanner.ScanText("89 54 24 10 53 55 57 41 54 41 55 41 56 48 83 EC 48 8B C2 45 8B E0 44 8B D2 45 32 F6 44 8B C2 45 32 ED");
                    PluginLog.Log(setOptionAddress.ToInt64().ToString("X"));
                    setOption = Marshal.GetDelegateForFunctionPointer<SetOptionDelegate>(setOptionAddress);
                }

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
            var change = false;

            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {
                ImGui.TextDisabled("/setoption list");
                ImGui.TextDisabled("/setoption [option] [value]");
                ImGui.TreePop();
            }

            return change;
        }

        public override unsafe void Enable() {
            if (!Ready) return;
            setOptionHook ??= new Hook<SetOptionDelegate>(setOptionAddress, new SetOptionDelegate(SetOptionDetour));
            setOptionHook?.Enable();

            PluginInterface.CommandManager.AddHandler("/setoption", new CommandInfo(OptionCommand) {HelpMessage = "Set the skill tooltips on or off.", ShowInHelp = true});

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
                    sb.Append(o + " ");
                }
                PluginInterface.Framework.Gui.Chat.Print($"Options:\n{sb}");

                return;
            }
            
            var optionKind = argList[0];

            if (!optionKinds.ContainsKey(optionKind)) {
                PluginInterface.Framework.Gui.Chat.PrintError("Unknown Option");
                PluginInterface.Framework.Gui.Chat.PrintError("/setoption list for a list of options");
                return;
            }

            var optionDefinition = optionKinds[optionKind];
            var optionValue = "";
            if (argList.Length >= 2) {
                optionValue = argList[1];
            }

            switch (optionDefinition.type) {
                
                case OptionType.Bool: {

                    switch (optionValue) {
                        case "1":
                        case "true":
                        case "on":
                            setOption(baseAddress, optionDefinition.key, 1, 2);
                            break;
                        case "0":
                        case "false":
                        case "off":
                            setOption(baseAddress, optionDefinition.key, 0, 2);
                            break;
                        case "t":
                        case "toggle":
                            if (optionDefinition.offset > 0) {
                                var cVal = Marshal.ReadByte(baseAddress, optionDefinition.offset);
                                setOption(baseAddress, optionDefinition.key, cVal == 1 ? 0UL : 1UL, 2);
                            } else {
                                PluginInterface.Framework.Gui.Chat.PrintError($"Toggle not available for {optionKind}");
                            }
                            break;
                        default:
                            PluginInterface.Framework.Gui.Chat.PrintError($"/setoption {optionKind} (on | off | toggle)");
                            break;
                        }

                    break;
                }
                default:
                    PluginInterface.Framework.Gui.Chat.PrintError($"Unsupported Option");
                    break;
            }

        }

        private IntPtr SetOptionDetour(IntPtr baseAddress, ulong kind, ulong value, ulong unknown) {
            this.baseAddress = baseAddress;
#if DEBUG
            PluginLog.Log($"{PluginInterface.Framework.Address.BaseAddress.ToInt64():X}");
            PluginLog.Log($"Set Option: {baseAddress.ToInt64():X} {kind:X}, {value:X}, {unknown:X}");
#endif
            return setOptionHook.Original(baseAddress, kind, value, unknown);
        }

        public override void Disable() {
            setOptionHook?.Disable();
            PluginInterface.CommandManager.RemoveHandler("/setoption");
            Enabled = false;
        }

        public override void Dispose() {
            setOptionHook?.Dispose();
            Enabled = false;
            Ready = false;
        }
    }
}
