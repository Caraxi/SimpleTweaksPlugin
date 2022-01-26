using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class ConfigDebug : DebugHelper {
        public override string Name => "ConfigDebug";

        private delegate byte SetOption(ConfigModule* configModule, uint index, int value, int a4, byte a5, byte a6);
        private HookWrapper<SetOption> setOptionHook;

        public override void Dispose() {
            setOptionHook?.Dispose();
            base.Dispose();
        }

        public override void Draw() {
            var config = Framework.Instance()->GetUiModule()->GetConfigModule();

            ImGui.Text("ConfigModule:");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong) config:X}");

            DebugManager.PrintOutObject(config);


            ImGui.Separator();

            if (ImGui.BeginTabBar("ConfigDebugTabs")) {

                if (ImGui.BeginTabItem("View")) {

                    for (var i = 0; i < ConfigModule.ConfigOptionCount; i++) {
                        
                    }




                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Changes")) {
                    var e = setOptionHook is { IsEnabled: true };

                    if (ImGui.Checkbox("Enable Config Change Logging", ref e)) {
                        if (e) {
                            setOptionHook ??= Common.Hook<SetOption>("E8 ?? ?? ?? ?? C6 47 4D 00", SetOptionDetour);
                            setOptionHook?.Enable();
                        } else {
                            setOptionHook?.Disable();
                        }
                    }
                    ImGui.Separator();

                    foreach (var change in changes) {
                        ImGui.Text($"[#{change.Index}] {change.Option} ({(short)change.Option}) => {change.Value}  [{change.a4}, {change.a5}, {change.a6}]");
                    }




                    ImGui.EndTabItem();
                }



                ImGui.EndTabBar();
            }
        }

        private List<LoggedConfigChange> changes = new List<LoggedConfigChange>();

        private class LoggedConfigChange {
            public uint Index;
            public ConfigOption Option;
            public int Value;
            public int a4;
            public byte a5;
            public byte a6;
        }

        private byte SetOptionDetour(ConfigModule* configmodule, uint index, int value, int a4, byte a5, byte a6) {
            try {
                var opt = configmodule->GetOption(index);
                changes.Insert(0, new LoggedConfigChange() {
                    Index = index,
                    Option = opt->OptionID,
                    Value = value,
                    a4 = a4,
                    a5 = a5,
                    a6 = a6
                });

                if (changes.Count > 200) changes.RemoveRange(200, changes.Count - 200);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }


            return setOptionHook.Original(configmodule, index, value, a4, a5, a6);

        }
    }
}
