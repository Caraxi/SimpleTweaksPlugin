using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.GameStructs.Client.UI.Client.UI.Misc;
using SimpleTweaksPlugin.GameStructs.Client.UI.VTable;
using SimpleTweaksPlugin.Helper;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class ActionBarDebug : DebugHelper {

        public override string Name => "Action Bar Debugging";

        private readonly string[] allActionBars = {
            "_ActionBar",
            "_ActionBar01",
            "_ActionBar02",
            "_ActionBar03",
            "_ActionBar04",
            "_ActionBar05",
            "_ActionBar06",
            "_ActionBar07",
            "_ActionBar08",
            "_ActionBar09",
            "_ActionCross",
            "_ActionDoubleCrossL",
            "_ActionDoubleCrossR",
        };

        private bool showUiNodes;
        
        public override void Draw() {
            ImGui.Text($"{Name} Debug");
            ImGui.Separator();

            var raptureHotbarModule = UiHelper.UiModule.RaptureHotbarModule;
            ImGui.Text("RaptureHotbarModule:");
            ImGui.SameLine();
            DebugUI.ClickToCopyText($"{(ulong)raptureHotbarModule:X}");
            ImGui.SameLine();
            ImGui.Text($"{Encoding.ASCII.GetString(raptureHotbarModule.Data->ModuleName, 15)}");
            
            ImGui.Separator();
            
            if (ImGui.BeginTabBar($"###{GetType().Name}_debug_tabs")) {
                if (ImGui.BeginTabItem("Normal")) {
                    DrawHotbarType(raptureHotbarModule, HotBarType.Normal);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Cross")) {
                    DrawHotbarType(raptureHotbarModule, HotBarType.Cross);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Pet")) {
                    DrawHotbarType(raptureHotbarModule, HotBarType.Pet, "Normal Pet", "Cross Pet");
                }
                
                ImGui.EndTabBar();
            }
        }

        private void DrawHotbarType(RaptureHotbarModule hotbarModule, HotBarType type, params string[] names) {
            if (ImGui.BeginTabBar("##hotbarTabs")) {
                for (var i = 0; i < hotbarModule.GetBarCount(type); i++) {
                    var tabName = names.Length > i ? names[i] : $"{i+1:00}";
                    if (ImGui.BeginTabItem($"{tabName}##hotbar{i}")) {
                        var hotbar = hotbarModule.GetBar(i, type);
                        if (hotbar != null) {
                            DrawHotbar(hotbarModule, hotbar);
                        }
                        ImGui.EndTabItem();
                    }
                
                }
                ImGui.EndTabBar();
            }
        }

        private void DrawHotbar(RaptureHotbarModule hotbarModule, HotBar* hotbar) {

            ImGui.Columns(8);
            ImGuiExt.SetColumnWidths(35);
            
            ImGui.Text($"##");
            ImGui.NextColumn();
            ImGui.Text("Command");
            ImGui.NextColumn();
            ImGui.Text("Icon");
            ImGui.NextColumn();
            ImGui.Text("Name");
            ImGui.NextColumn();
            
            
            ImGuiExt.NextRow();
            ImGui.Separator();
            ImGui.Separator();
            
            
            for (var i = 0; i < 16; i++) {
                var slot = hotbarModule.GetBarSlot(hotbar, i);
                if (slot == null) break;
                if (slot->CommandType == HotbarSlotType.Empty) {
                    ImGui.PushStyleColor(ImGuiCol.Text, slot->CommandType == HotbarSlotType.Empty ? 0x99999999 : 0xFFFFFFFF);
                    DebugUI.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
                    ImGui.NextColumn();
                    ImGui.Text("Empty");
                    ImGui.PopStyleColor();
                    ImGuiExt.NextRow();
                    ImGui.Separator();
                    continue;
                }
                
                DebugUI.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
                
                ImGui.NextColumn();
                
                ImGui.Text($"{slot->CommandType} : {slot->CommandId}");
                ImGui.NextColumn();

                ImGui.Text($"{slot->IconTypeA} : {slot->IconA}");
                ImGui.Text($"{slot->IconTypeB} : {slot->IconB}");
                
                ImGui.NextColumn();
                switch (slot->CommandType) {
                    case HotbarSlotType.Empty: { break; }
                    case HotbarSlotType.Action: {

                        var action = Plugin.PluginInterface.Data.Excel.GetSheet<Action>().GetRow(slot->CommandId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{action.Name}");
                        }
                        break;
                    }

                    case HotbarSlotType.Item: {
                        var item = Plugin.PluginInterface.Data.GetExcelSheet<Item>().GetRow(slot->CommandId);
                        if (item == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{item.Name}");
                        }
                        break;
                    }

                    default: {
                        ImGui.TextDisabled("Name Not Supprorted");
                        break;
                    }
                }
                
                
                
                ImGuiExt.NextRow();
                ImGui.Separator();
                

            }
            ImGui.Columns();
        }
    }
}
