using System.Collections.Generic;
using System.Text;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Excel;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class ExcelSheetDebugging : DebugHelper {
        public override string Name => "ExcelSheetDebugging";

        private Dictionary<string, ulong> byNameCounts = new();
        private Dictionary<uint, ulong> byIndexCounts = new();

        private Dictionary<string, ulong> byNameValues = new();
        private Dictionary<uint, ulong> byIndexValues = new();


        private delegate ExcelSheet* ByNameDelegate(ExcelModule* excelModule, string name);
        private delegate ExcelSheet* ByIndexDelegate(ExcelModule* excelModule, uint id);


        private HookWrapper<ByNameDelegate> byNameHook;
        private HookWrapper<ByIndexDelegate> byIndexHook;

        private bool enabled = false;

        public override void Draw() {

            if (ImGui.Checkbox("Enable Logging", ref enabled)) {
                byNameHook?.Disable();
                byIndexHook?.Disable();
                if (enabled) {

                    byNameHook ??= Common.Hook<ByNameDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 48 8B D9 48 8B F2", ByNameDetour);
                    byIndexHook ??= Common.Hook<ByIndexDelegate>("4C 8B 81 ?? ?? ?? ?? 4D 85 C0 74 07", ByIndexDetour);

                    byNameHook?.Enable();
                    byIndexHook?.Enable();


                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset")) {
                byNameCounts.Clear();
                byIndexCounts.Clear();
            }

            ImGui.Separator();


            if (ImGui.BeginTable("resultsTable_excelSheetDebugging", 5)) {

                ImGui.TableSetupColumn("Request ID/Name", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Hit Count", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Sheet Name", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Data");

                ImGui.TableHeadersRow();

                foreach (var (key, value) in byIndexCounts) {
                    ImGui.TableNextColumn();
                    ImGui.Text($"{key}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{value}");
                    ImGui.TableNextColumn();
                    if (byIndexValues.ContainsKey(key)) {
                        var e = (ExcelSheet*)byIndexValues[key];
                        var name = Encoding.UTF8.GetString(e->SheetName, 16).TrimEnd();
                        ImGui.Text($"{name}");
                        ImGui.TableNextColumn();

                        DebugManager.ClickToCopyText($"{byIndexValues[key]:X}");
                        ImGui.TableNextColumn();
                        DebugManager.PrintOutObject(e);
                    } else {
                        ImGui.TableNextRow();
                    }
                }

                ImGui.TableNextRow();
                ImGui.Separator();
                ImGui.TableNextRow();



                foreach (var (key, value) in byNameCounts) {
                    ImGui.TableNextColumn();
                    ImGui.Text($"{key}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{value}");
                    ImGui.TableNextColumn();
                    if (byNameValues.ContainsKey(key)) {
                        var e = (ExcelSheet*)byNameValues[key];
                        if (e->SheetName == null) {
                            ImGui.Text($"[no name]");
                        } else {
                            var name = Encoding.UTF8.GetString(e->SheetName, 32).TrimEnd();
                            ImGui.Text($"{name}");
                        }


                        ImGui.TableNextColumn();
                        DebugManager.ClickToCopyText($"{byNameValues[key]:X}");
                        ImGui.TableNextColumn();
                        DebugManager.PrintOutObject(e);
                    } else {
                        ImGui.TableNextRow();
                    }

                }



                ImGui.EndTable();
            }
        }

        private ExcelSheet* ByNameDetour(ExcelModule* excelModule, string name) {
            if (!byNameCounts.ContainsKey(name)) byNameCounts.Add(name, 0);
            byNameCounts[name]++;
            var ret = byNameHook.Original(excelModule, name);

            if (byNameValues.ContainsKey(name))
                byNameValues[name] = (ulong) ret;
            else
                byNameValues.Add(name, (ulong) ret);

            return ret;
        }

        private ExcelSheet* ByIndexDetour(ExcelModule* excelModule, uint index) {
            if (!byIndexCounts.ContainsKey(index)) byIndexCounts.Add(index, 0);
            byIndexCounts[index]++;
            var ret = byIndexHook.Original(excelModule, index);
            if (byIndexValues.ContainsKey(index))
                byIndexValues[index] = (ulong) ret;
            else
                byIndexValues.Add(index, (ulong) ret);
            return ret;
        }

        public override void Dispose() {
            byNameHook?.Disable();
            byNameHook?.Dispose();
            byIndexHook?.Disable();
            byIndexHook?.Dispose();
            base.Dispose();
        }
    }
}
