using System;
using System.Collections.Generic;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class ConfigDebug : DebugHelper {
    public override string Name => "ConfigDebug";

    private delegate byte SetOption(ConfigModule* configModule, uint index, int value, int a4, byte a5, byte a6);
    private HookWrapper<SetOption> setOptionHook;

    public override void Dispose() {
        setOptionHook?.Dispose();
        base.Dispose();
    }

    private string searchString = string.Empty;

    public override void Draw() {
        var config = Framework.Instance()->GetUiModule()->GetConfigModule();

        ImGui.Text("ConfigModule:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong) config:X}");

        DebugManager.PrintOutObject(config);


        ImGui.Separator();

        if (ImGui.BeginTabBar("ConfigDebugTabs")) {

            if (ImGui.BeginTabItem("View")) {

                ImGui.InputText("Search Option", ref searchString, 50);

                if (ImGui.BeginTable("configViewTable", 5)) {
                    ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Option Name", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 120);
                    ImGui.TableSetupColumn("Value2", ImGuiTableColumnFlags.WidthFixed, 120);
                    ImGui.TableHeadersRow();

                    for (short i = 0; i < 2000; i++) {

                        var c = config->GetOptionById(i);
                        if (c == null) continue;

                        if (!string.IsNullOrWhiteSpace(searchString)) {
                            if (!$"{c->OptionID}".Contains(searchString, StringComparison.OrdinalIgnoreCase)) continue;
                        }

                        var v = config->GetValue(c->OptionID);
                        ImGui.TableNextColumn();
                        ImGui.Text($"#{i}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{c->OptionID}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{v->Type}");
                        ImGui.TableNextColumn();

                        void PrintValue(AtkValue* atkValue) {
                            switch (atkValue->Type) {
                                case ValueType.Int:
                                    ImGui.Text($"{atkValue->Int}");
                                    break;
                                case ValueType.Bool:
                                    ImGui.Text($"{atkValue->Byte == 1}");
                                    break;
                                case ValueType.UInt:
                                    ImGui.Text($"{atkValue->UInt}");
                                    break;
                                case ValueType.Float:
                                    ImGui.Text($"{atkValue->Float}");
                                    break;
                                case ValueType.AllocatedString:
                                case ValueType.String:
                                    var str = MemoryHelper.ReadStringNullTerminated(new IntPtr(atkValue->String));
                                    ImGui.Text($"{str}");
                                    break;
                                case ValueType.AllocatedVector:
                                case ValueType.Vector:
                                    var vec = atkValue->Vector;
                                    foreach (var vecValue in vec->Span) {
                                        PrintValue(&vecValue);
                                    }
                                    break;
                                default:
                                    ImGui.Text($"Unknown Value Type: {atkValue->Type}");
                                    break;
                            }
                        }
                        PrintValue(v);

                        ImGui.TableNextColumn();
                        var intVal = config->GetIntValue(c->OptionID);
                        ImGui.Text($"{intVal}");

                    }

                    ImGui.EndTable();
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