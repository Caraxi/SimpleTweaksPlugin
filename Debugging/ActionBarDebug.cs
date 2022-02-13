
using System.Linq;
using System.Numerics;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using SimpleTweaksPlugin.Helper;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class ActionBarDebug : DebugHelper {

    public override string Name => "Action Bar Debugging";
        
    public override void Draw() {
        ImGui.Text($"{Name} Debug");
        ImGui.Separator();

        var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        ImGui.Text("RaptureHotbarModule:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)raptureHotbarModule:X}");
        ImGui.SameLine();
        ImGui.Text("ActionManager:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)ActionManager.Instance():X}");
            
        ImGui.Separator();


        if (ImGui.BeginTabBar("##hotbarDebugDisplay")) {

            if (ImGui.BeginTabItem("Current Bars")) {
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
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Saved Bars")) {

                if (ImGui.BeginChild("savedBarsIndexSelect", new Vector2(150, -1) * ImGui.GetIO().FontGlobalScale, true)) {
                    for (byte i = 0; i < 61; i++) {
                        var cj = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(i)?.Abbreviation?.RawString;

                        if (i > 41) {
                            cj = Service.Data.Excel.GetSheet<ClassJob>()?.Where(j => j.IsLimitedJob == false && j.JobIndex > 0).Skip(i - 42).FirstOrDefault()?.Abbreviation?.RawString;
                        }

                        if (ImGui.Selectable((i > 40 ? "[PVP] " : "") + (i is 0 or 41 ? "Shared" : cj ?? $"{i}"), selectedSavedIndex == i)) {
                            selectedSavedIndex = i;
                        }
                    }
                }
                ImGui.EndChild();
                ImGui.SameLine();
                ImGui.BeginGroup();
                var savedBarClassJob = raptureHotbarModule->SavedClassJob[selectedSavedIndex];
                if (savedBarClassJob != null && ImGui.BeginTabBar("savedClassJobBarSelectType")) {


                    void ShowBar(int b) {

                        var savedBar = savedBarClassJob->Bar[b];
                        if (savedBar == null) {
                            ImGui.Text("Bar is Null");
                            return;
                        }

                        if (ImGui.BeginTable("savedClassJobBarSlots", 3)) {

                            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 100);

                            ImGui.TableHeadersRow();

                            for (var i = 0; i < 16; i++) {
                                ImGui.TableNextColumn();
                                ImGui.Text($"{i:00}");
                                ImGui.TableNextColumn();
                                var slot = savedBar->Slot[i];
                                if (slot == null) {
                                    ImGui.TableNextRow();
                                    continue;
                                }
                                ImGui.Text($"{slot->Type}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{slot->ID}");
                            }

                            ImGui.EndTable();
                        }



                    }

                    if (ImGui.BeginTabItem("Normal")) {
                        if (ImGui.BeginTabBar("savecClassJobBarSelectCross")) {
                            for (var i = 0; i < 10; i++) {
                                if (ImGui.BeginTabItem($"{i + 1:00}")) {
                                    ShowBar(i);
                                    ImGui.EndTabItem();
                                }
                            }
                            ImGui.EndTabBar();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Cross")) {
                        if (ImGui.BeginTabBar("savecClassJobBarSelectCross")) {
                            for (var i = 10; i < 18; i++) {
                                if (ImGui.BeginTabItem($"{i-9:00}")) {
                                    ShowBar(i);
                                    ImGui.EndTabItem();
                                }
                            }
                            ImGui.EndTabBar();
                        }

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }


                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }



    }

    private int selectedSavedIndex = 0;


    public class HotBarType {

        public static HotBarType Normal => new HotBarType() { Count = 10, FirstIndex = 0 };
        public static HotBarType Cross => new HotBarType() { Count = 8, FirstIndex = 10 };
        public static HotBarType Pet => new HotBarType() { Count = 2, FirstIndex = 18 };


        public int Count;
        public int FirstIndex;
    }


    private void DrawHotbarType(RaptureHotbarModule* hotbarModule, HotBarType type, params string[] names) {
        if (ImGui.BeginTabBar("##hotbarTabs")) {
            for (var i = 0; i < type.Count; i++) {
                var tabName = names.Length > i ? names[i] : $"{i+1:00}";
                if (ImGui.BeginTabItem($"{tabName}##hotbar{i}")) {
                    var hotbar = hotbarModule->HotBar[type.FirstIndex + i];
                    if (hotbar != null) {
                        DrawHotbar(hotbarModule, hotbar);
                    }
                    ImGui.EndTabItem();
                }

            }
            ImGui.EndTabBar();
        }
    }

    private void DrawHotbar(RaptureHotbarModule* hotbarModule, HotBar* hotbar) {

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
            var slot = hotbar->Slot[i];
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
                
            var adjustedId = slot->CommandType == HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;

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
                    var gearsetModule = RaptureGearsetModule.Instance();
                    var gearset = gearsetModule->Gearset[(int)slot->CommandId];
                    ImGui.Text($"{Encoding.UTF8.GetString(gearset->Name, 0x2F)}");
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
                        
                    var cdg = ActionManager.Instance()->GetRecastGroup(2, slot->CommandId);
                    if (cdg < 81) cooldownGroup = (int) (cdg + 1);
                        
                    break;
                }
                case HotbarSlotType.GeneralAction: {
                    var action = Service.Data.Excel.GetSheet<GeneralAction>().GetRow(slot->CommandId);
                    if (action?.Action == null) {
                        ImGui.TextDisabled("Not Found");
                        break;
                    }

                    cooldownGroup = ActionManager.Instance()->GetRecastGroup(5, slot->CommandId);
                    break;
                }
            }

            if (cooldownGroup > 0) {
                    
                ImGui.Text($"Cooldown Group: {cooldownGroup}");

                var cooldown = ActionManager.Instance()->GetRecastGroupDetail(cooldownGroup);
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