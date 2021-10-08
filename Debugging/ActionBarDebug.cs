using System.Numerics;
using System.Text;
using FFXIVClientInterface.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using SimpleTweaksPlugin.Helper;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class ActionBarDebug : DebugHelper {

        public override string Name => "Action Bar Debugging";
        
        public override void Draw() {
            ImGui.Text($"{Name} Debug");
            ImGui.Separator();

            var raptureHotbarModule = SimpleTweaksPlugin.Client.UiModule.RaptureHotbarModule;
            ImGui.Text("RaptureHotbarModule:");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)raptureHotbarModule:X}");
            ImGui.SameLine();
            ImGui.Text($"{Encoding.ASCII.GetString(raptureHotbarModule.Data->ModuleName, 15)}");
            ImGui.Text("ActionManager:");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)SimpleTweaksPlugin.Client.ActionManager.Data:X}");
            
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
            ImGui.Text("Cooldown");
            ImGui.NextColumn();
            
            
            ImGuiExt.NextRow();
            ImGui.Separator();
            ImGui.Separator();
            
            
            for (var i = 0; i < 16; i++) {
                var slot = hotbarModule.GetBarSlot(hotbar, i);
                if (slot == null) break;
                if (slot->CommandType == HotbarSlotType.Empty) {
                    ImGui.PushStyleColor(ImGuiCol.Text, slot->CommandType == HotbarSlotType.Empty ? 0x99999999 : 0xFFFFFFFF);
                    DebugManager.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
                    ImGui.NextColumn();
                    ImGui.Text("Empty");
                    ImGui.PopStyleColor();
                    ImGuiExt.NextRow();
                    ImGui.Separator();
                    continue;
                }
                
                var adjustedId = slot->CommandType == HotbarSlotType.Action ? SimpleTweaksPlugin.Client.ActionManager.GetAdjustedActionId((int)slot->CommandId) : slot->CommandId;

                DebugManager.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
                
                ImGui.NextColumn();
                
                ImGui.Text($"{slot->CommandType} : {slot->CommandId}");
                if (slot->CommandType == HotbarSlotType.Action) {
                    ImGui.Text($"Adjusted: {adjustedId}");
                }
                ImGui.NextColumn();

                var iconGood = false;
                if (slot->Icon >= 0) {
                    var icon = Plugin.IconManager.GetIconTexture(slot->Icon % 1000000, slot->Icon >= 1000000);
                    if (icon != null) {
                        ImGui.Image(icon.ImGuiHandle, new Vector2(32));
                        iconGood = true;
                    }
                }
                if (!iconGood) {
                    ImGui.GetWindowDrawList().AddRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(32), 0xFF0000FF, 4);
                    ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos(), 0xFFFFFFFF, $"{slot->Icon}");
                   
                    ImGui.Dummy(new Vector2(32));
                }
                ImGui.SameLine();
                
                ImGui.Text($"{slot->IconTypeA} : {slot->IconA}\n{slot->IconTypeB} : {slot->IconB}");

                ImGui.NextColumn();
                switch (slot->CommandType) {
                    case HotbarSlotType.Empty: { break; }
                    case HotbarSlotType.Action: {

                        var action = Service.Data.Excel.GetSheet<Action>().GetRow(slot->CommandId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{action.Name}");
                        }
                        break;
                    }

                    case HotbarSlotType.Item: {
                        var item = Service.Data.GetExcelSheet<Item>().GetRow(slot->CommandId % 500000);
                        if (item == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{item.Name}");
                        }
                        break;
                    }

                    case HotbarSlotType.CraftAction: {
                        var action = Service.Data.GetExcelSheet<CraftAction>().GetRow(slot->CommandId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{action.Name}");
                        }
                        break;
                    }

                    case HotbarSlotType.GeneralAction: {
                        var action = Service.Data.GetExcelSheet<GeneralAction>().GetRow(slot->CommandId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{action.Name}");
                        }
                        break;
                    }
                    
                    case HotbarSlotType.MainCommand: {
                        var action = Service.Data.GetExcelSheet<MainCommand>().GetRow(slot->CommandId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{action.Name}");
                        }
                        break;
                    }
                    
                    case HotbarSlotType.ExtraCommand: {
                        var rawSheet = Service.Data.Excel.GetSheetRaw("ExtraCommand");
                        var parser = rawSheet.GetRowParser(slot->CommandId);
                        var name = parser.ReadColumn<SeString>(0);
                        ImGui.Text($"{name}");
                        break;
                    }

                    case HotbarSlotType.GearSet: {
                        var gearsetModule = SimpleTweaksPlugin.Client.UiModule.RaptureGearsetModule;
                        var gearset = gearsetModule.Gearset[slot->CommandId];
                        ImGui.Text($"{Encoding.UTF8.GetString(gearset.Name, 0x2F)}");
                        break;
                    }

                    case HotbarSlotType.Macro: {
                        ImGui.Text($"{(slot->CommandId >= 256 ? "Shared" : "Individual")} #{slot->CommandId%256}");
                        break;
                    }

                    case HotbarSlotType.Emote: {
                        ImGui.Text($"{Service.Data.Excel.GetSheet<Emote>().GetRow(slot->CommandId)?.Name ?? "Invalid"}");
                        break;
                    }
                    
                    case HotbarSlotType.EventItem: {
                        var item = Service.Data.GetExcelSheet<EventItem>().GetRow(slot->CommandId);
                        if (item == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{item.Name}");
                        }
                        break;
                    }
                    
                    case HotbarSlotType.Mount: {
                        var m = Service.Data.Excel.GetSheet<Mount>().GetRow(slot->CommandId);
                        if (m == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{m.Singular}");
                        }

                        break;
                    }

                    case HotbarSlotType.Minion: {
                        var m = Service.Data.Excel.GetSheet<Companion>().GetRow(slot->CommandId);
                        if (m == null) {
                            ImGui.TextDisabled("Not Found");
                        } else {
                            ImGui.TextWrapped($"{m.Singular}");
                        }

                        break;
                    }
                    
                    default: {
                        ImGui.TextDisabled("Name Not Supprorted");
                        break;
                    }
                }
                
                ImGui.NextColumn();

                var cooldownGroup = -1;
                
                switch (slot->CommandType) {
                    case HotbarSlotType.Action: {
                        var action = Service.Data.Excel.GetSheet<Action>().GetRow((uint)adjustedId);
                        if (action == null) {
                            ImGui.TextDisabled("Not Found");
                            break;
                        }
                        cooldownGroup = action.CooldownGroup;
                        break;
                    }
                    case HotbarSlotType.Item: {
                        var item = Service.Data.Excel.GetSheet<Item>().GetRow(slot->CommandId);
                        if (item == null) {
                            ImGui.TextDisabled("Not Found");
                            break;
                        }
                        
                        var cdg = SimpleTweaksPlugin.Client.ActionManager.GetRecastGroup(2, slot->CommandId);
                        if (cdg < 81) cooldownGroup = (int) (cdg + 1);
                        
                        break;
                    }
                    case HotbarSlotType.GeneralAction: {
                        var action = Service.Data.Excel.GetSheet<GeneralAction>().GetRow(slot->CommandId);
                        if (action?.Action == null) {
                            ImGui.TextDisabled("Not Found");
                            break;
                        }

                        cooldownGroup = action.Action.Value.CooldownGroup;
                        break;
                    }
                }

                if (cooldownGroup > 0) {
                    
                    ImGui.Text($"Cooldown Group: {cooldownGroup}");

                    var cooldown = SimpleTweaksPlugin.Client.ActionManager.GetGroupRecastTime(cooldownGroup);
                    DebugManager.ClickToCopyText($"{(ulong)cooldown:X}");
                    if (cooldown != null) {
                        ImGui.Text($"{cooldown->IsActive} / {cooldown->Elapsed} / {cooldown->Total}");
                    } else {
                        ImGui.Text("Failed");
                    }
                }
                
                ImGuiExt.NextRow();
                ImGui.Separator();
                

            }
            ImGui.Columns();
        }
    }
}
