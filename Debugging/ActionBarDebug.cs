using System.Numerics;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using JetBrains.Annotations;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class ActionBarDebug : DebugHelper {

    public override string Name => "Action Bar Debugging";
        
    public override void Draw() {
        var raptureHotbarModule = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
        ImGui.Text("RaptureHotbarModule:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)raptureHotbarModule:X}");
        ImGui.SameLine();
        ImGui.Text("ActionManager:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)ActionManager.Instance():X}");

        DebugManager.PrintOutObject(raptureHotbarModule);
            
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

                    ImGui.EndTabBar();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Saved Bars")) {
                var classJobSheet = Service.Data.GetExcelSheet<ClassJob>()!;
                
                if (ImGui.BeginChild("savedBarsIndexSelect", new Vector2(150, -1) * ImGui.GetIO().FontGlobalScale, true)) {
                    for (byte i = 0; i < raptureHotbarModule->SavedHotBars.Length; i++) {
                        var classJobId = raptureHotbarModule->GetClassJobIdForSavedHotbarIndex(i);
                        var jobName = classJobId == 0 ? "Shared" : classJobSheet.GetRow(classJobId)?.Abbreviation?.RawString;
                        var isPvp = i >= classJobSheet.RowCount;
                        
                        // hack for unreleased jobs
                        if (jobName.IsNullOrEmpty() || (i > classJobSheet.RowCount && classJobId == 0)) jobName = "Unknown";
                        
                        if (ImGui.Selectable($"{i}: {(isPvp ? "[PVP]" : "")} {jobName}", selectedSavedIndex == i)) {
                            selectedSavedIndex = i;
                        }
                    }
                }
                ImGui.EndChild();
                ImGui.SameLine();
                ImGui.BeginGroup();
                var savedBarClassJob = raptureHotbarModule->SavedHotBars.GetPointer(selectedSavedIndex);
                if (savedBarClassJob != null && ImGui.BeginTabBar("savedClassJobBarSelectType")) {


                    void ShowBar(int b) {

                        var savedBar = savedBarClassJob->HotBars.GetPointer(b);
                        if (savedBar == null) {
                            ImGui.Text("Bar is Null");
                            return;
                        }

                        if (ImGui.BeginTable("savedClassJobBarSlots", 4)) {

                            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Resolved Name", ImGuiTableColumnFlags.WidthStretch, 128);

                            ImGui.TableHeadersRow();

                            for (var i = 0; i < 16; i++) {
                                ImGui.TableNextColumn();
                                ImGui.Text($"{i:00}");
                                ImGui.TableNextColumn();
                                var slot = savedBar->Slots.GetPointer(i);
                                if (slot == null) {
                                    ImGui.TableNextRow();
                                    continue;
                                }
                                ImGui.Text($"{slot->CommandType}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{slot->CommandId}");
                                ImGui.TableNextColumn();
                                if (this.ResolveSlotName(slot->CommandType, slot->CommandId, out var resolvedName)) {
                                    ImGui.TextWrapped(resolvedName);
                                } else {
                                    ImGui.TextDisabled(resolvedName);
                                }
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

    public enum HotBarType {
        Normal,
        Cross,
    }

    private void DrawHotbarType(RaptureHotbarModule* hotbarModule, HotBarType type) {
        var isNormalBar = type == HotBarType.Normal;
        var baseSpan = isNormalBar ? hotbarModule->StandardHotBars : hotbarModule->CrossHotBars;
        
        if (ImGui.BeginTabBar("##hotbarTabs")) {
            for (var i = 0; i < baseSpan.Length; i++) {
                if (ImGui.BeginTabItem($"{i+1:00}##hotbar{i}")) {
                    var hotbar = baseSpan.GetPointer(i);
                    if (hotbar != null) {
                        DrawHotbar(hotbarModule, hotbar);
                    }

                    if (isNormalBar) {
                        this.DrawAddonInfo(i == 0 ? "_ActionBar" : $"_ActionBar{i:00}");
                    }
                    
                    ImGui.EndTabItem();
                }

            }
            
            // Pet hotbar is a special case
            if (ImGui.BeginTabItem("Pet##hotbarex")) {
                
                var petBar = isNormalBar ? &hotbarModule->PetHotBar : &hotbarModule->PetCrossHotBar;
                DrawHotbar(hotbarModule, petBar);

                if (isNormalBar) {
                    this.DrawAddonInfo("_ActionBarEx");
                }
                
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawHotbar(RaptureHotbarModule* hotbarModule, RaptureHotbarModule.HotBar* hotbar) {
        using var tableBorderLight = ImRaii.PushColor(ImGuiCol.TableBorderLight, ImGui.GetColorU32(ImGuiCol.Border));
        using var tableBorderStrong = ImRaii.PushColor(ImGuiCol.TableBorderStrong, ImGui.GetColorU32(ImGuiCol.Border));
        if (!ImGui.BeginTable("HotbarTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) return;
        
        ImGui.TableSetupColumn("##", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 180);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180);
        ImGui.TableSetupColumn("Cooldown", ImGuiTableColumnFlags.WidthFixed, 180);
        ImGui.TableSetupColumn("Struct", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        
        for (var i = 0; i < 16; i++) {
            var slot = hotbar->Slots.GetPointer(i);
            if (slot == null) break;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty) {
                ImGui.PushStyleColor(ImGuiCol.Text, slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty ? 0x99999999 : 0xFFFFFFFF);
                DebugManager.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
                ImGui.SameLine();
                ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeight() * 4));
                ImGui.TableNextColumn();
                ImGui.Text("Empty");
                ImGui.PopStyleColor();
                continue;
            }
                
            var adjustedId = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
            DebugManager.ClickToCopyText($"{i+1:00}", $"{(ulong)slot:X}");
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeight() * 4));
            ImGui.TableNextColumn();
                
            ImGui.Text($"{slot->CommandType} : {slot->CommandId}");
            if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action) {
                ImGui.Text($"Adjusted: {adjustedId}");
            }

            if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Macro) {
                ImGui.Text($"{(slot->CommandId >= 256 ? "Shared" : "Individual")} #{slot->CommandId % 256}");
            }
            
            ImGui.TableNextColumn();

            var iconGood = false;
            if (slot->IconId >= 0) {
                var icon = Plugin.IconManager.GetIconTexture((uint)(slot->IconId % 1000000), slot->IconId >= 1000000);
                if (icon != null) {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(32));
                    iconGood = true;
                }
            }
            if (!iconGood) {
                ImGui.GetWindowDrawList().AddRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(32), 0xFF0000FF, 4);
                ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos(), 0xFFFFFFFF, $"{slot->IconId}");
                   
                ImGui.Dummy(new Vector2(32));
            }
            ImGui.SameLine();
                
            ImGui.Text($"A: {slot->OriginalApparentSlotType}#{slot->OriginalApparentActionId}\nB: {slot->ApparentSlotType}#{slot->ApparentActionId}");

            // Column "Name"
            ImGui.TableNextColumn();
            
            var popUpHelp = SeString.Parse(slot->PopUpHelp).ToString();
            if (popUpHelp.IsNullOrEmpty()) {
                ImGui.TextDisabled("Empty PopUpHelp");
            } else {
                ImGui.TextWrapped(popUpHelp);
            }

            if (this.ResolveSlotName(slot->CommandType, slot->CommandId, out var resolvedName)) {
                ImGui.TextWrapped($"Resolved: {resolvedName}");
            } else {
                ImGui.TextDisabled($"Resolved: {resolvedName}");
            }
                
            // Column "Cooldown"
            ImGui.TableNextColumn();

            var cooldownGroup = -1;
                
            switch (slot->CommandType) {
                case RaptureHotbarModule.HotbarSlotType.Action: {
                    var action = Service.Data.Excel.GetSheet<Action>()!.GetRow(adjustedId);
                    if (action == null) {
                        ImGui.TextDisabled("Not Found");
                        break;
                    }
                    cooldownGroup = action.CooldownGroup;
                    break;
                }
                case RaptureHotbarModule.HotbarSlotType.Item: {
                    var item = Service.Data.Excel.GetSheet<Item>()!.GetRow(slot->CommandId);
                    if (item == null) {
                        ImGui.TextDisabled("Not Found");
                        break;
                    }
                        
                    var cdg = ActionManager.Instance()->GetRecastGroup(2, slot->CommandId);
                    if (cdg < 81) cooldownGroup = cdg + 1;
                        
                    break;
                }
                case RaptureHotbarModule.HotbarSlotType.GeneralAction: {
                    var action = Service.Data.Excel.GetSheet<GeneralAction>()!.GetRow(slot->CommandId);
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
            
            // Column "Struct"
            ImGui.TableNextColumn();
            DebugManager.PrintOutObject(slot);
        }
        
        ImGui.EndTable();
        
    }

    private void DrawAddonInfo(string addonName) {
        var addon = Common.GetUnitBase<AddonActionBarBase>(addonName);
        
        ImGui.Dummy(new Vector2(50));
        ImGui.Separator();
        
        if (addon != null) {
            ImGui.Text($"Shared: {addon->IsSharedHotbar}");
            ImGui.Text($"Slot Count: {addon->SlotCount}");
                        
            ImGui.Dummy(new Vector2(50));
            UIDebug.DrawUnitBase(&addon->AtkUnitBase);
        } else {
            ImGui.TextDisabled($"Couldn't get addon {addonName}");
        }
    }

    private bool ResolveSlotName(RaptureHotbarModule.HotbarSlotType type, uint commandId, [CanBeNull] out string resolvedName) {
        resolvedName = "Not Found";

        switch (type) {
            case RaptureHotbarModule.HotbarSlotType.Empty: {
                resolvedName = "N/A";
                return false;
            }
            case RaptureHotbarModule.HotbarSlotType.Action: {

                var action = Service.Data.Excel.GetSheet<Action>()!.GetRow(commandId);
                if (action == null) {
                    return false;
                }

                resolvedName = action.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Item: {
                var item = Service.Data.GetExcelSheet<Item>()!.GetRow(commandId % 500000);
                if (item == null) {
                    return false;
                }

                resolvedName = item.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.CraftAction: {
                var action = Service.Data.GetExcelSheet<CraftAction>()!.GetRow(commandId);
                if (action == null) {
                    return false;
                }

                resolvedName = action.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.GeneralAction: {
                var action = Service.Data.GetExcelSheet<GeneralAction>()!.GetRow(commandId);
                if (action == null) {
                    return false;
                }

                resolvedName = action.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.MainCommand: {
                var action = Service.Data.GetExcelSheet<MainCommand>()!.GetRow(commandId);
                if (action == null) {
                    return false;
                }

                resolvedName = action.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.ExtraCommand: {
                var exc = Service.Data.GetExcelSheet<ExtraCommand>()!.GetRow(commandId);
                if (exc == null) {
                    return false;
                }

                resolvedName = exc.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.GearSet: {
                var gearsetModule = RaptureGearsetModule.Instance();
                var gearset = gearsetModule->GetGearset((int)commandId);

                if (gearset == null) {
                    resolvedName = $"InvalidGearset#{commandId}";
                    return false;
                }

                resolvedName = $"{gearset->NameString}";
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Macro: {
                var macroModule = RaptureMacroModule.Instance();
                var macro = macroModule->GetMacro(commandId / 256, commandId % 256);
                
                if (macro == null) {
                    return false;
                }

                var macroName = macro->Name.ToString();
                if (macroName.IsNullOrEmpty()) {
                    macroName = $"{(commandId >= 256 ? "Shared" : "Individual")} #{commandId % 256}";
                }
                
                resolvedName = macroName;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Emote: {
                var m = Service.Data.GetExcelSheet<Emote>()!.GetRow(commandId);
                if (m == null) {
                    return false;
                }

                resolvedName = m.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.EventItem: {
                var item = Service.Data.GetExcelSheet<EventItem>()!.GetRow(commandId);
                if (item == null) {
                    return false;
                }

                resolvedName = $"{item.Name}";
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Mount: {
                var m = Service.Data.Excel.GetSheet<Mount>()!.GetRow(commandId);
                if (m == null) {
                    return false;
                }

                resolvedName = $"{m.Singular}";
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Companion: {
                var m = Service.Data.Excel.GetSheet<Companion>()!.GetRow(commandId);
                if (m == null) {
                    return false;
                }

                resolvedName = $"{m.Singular}";
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.McGuffin: {
                var c = Service.Data.Excel.GetSheet<McGuffin>()!.GetRow(commandId);
                if (c == null) {
                    return false;
                }

                resolvedName = c.UIData.Value!.Name;
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.PetAction: {
                var pa = Service.Data.GetExcelSheet<PetAction>()!.GetRow(commandId);
                if (pa == null) {
                    return false;
                }

                resolvedName = pa.Name;
                return true;
            }
            
            default: {
                resolvedName = "Not Yet Supported";
                return false;
            }
        }
    }
}