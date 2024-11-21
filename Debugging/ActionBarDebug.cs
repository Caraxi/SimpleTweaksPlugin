using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using JetBrains.Annotations;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;

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
                    for (byte i = 0; i < raptureHotbarModule->SavedHotbars.Length; i++) {
                        var classJobId = raptureHotbarModule->GetClassJobIdForSavedHotbarIndex(i);
                        
                        var jobName = classJobId == 0 ? "Shared" : $"Unk#{classJobId}";
                        if (classJobId != 0 && classJobSheet.TryGetRow(classJobId, out var cjRow)) {
                            jobName = cjRow.Abbreviation.ExtractText();
                        }
                        
                        var isPvp = i >= classJobSheet.Count;

                        // hack for unreleased jobs
                        if (jobName.IsNullOrEmpty() || (i > classJobSheet.Count && classJobId == 0)) jobName = "Unknown";

                        if (ImGui.Selectable($"{i}: {(isPvp ? "[PVP]" : "")} {jobName}", selectedSavedIndex == i)) {
                            selectedSavedIndex = i;
                        }
                    }
                }

                ImGui.EndChild();
                ImGui.SameLine();
                ImGui.BeginGroup();
                var savedBarClassJob = raptureHotbarModule->SavedHotbars.GetPointer(selectedSavedIndex);
                if (savedBarClassJob != null && ImGui.BeginTabBar("savedClassJobBarSelectType")) {
                    void ShowBar(int b) {
                        var savedBar = savedBarClassJob->Hotbars.GetPointer(b);
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
                                if (ImGui.BeginTabItem($"{i - 9:00}")) {
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

    private int selectedSavedIndex;

    public enum HotBarType {
        Normal,
        Cross,
    }

    private void DrawHotbarType(RaptureHotbarModule* hotbarModule, HotBarType type) {
        var isNormalBar = type == HotBarType.Normal;
        var baseSpan = isNormalBar ? hotbarModule->StandardHotbars : hotbarModule->CrossHotbars;

        if (ImGui.BeginTabBar("##hotbarTabs")) {
            for (var i = 0; i < baseSpan.Length; i++) {
                if (ImGui.BeginTabItem($"{i + 1:00}##hotbar{i}")) {
                    var hotbar = baseSpan.GetPointer(i);
                    if (hotbar != null) {
                        DrawHotbar(hotbar);
                    }

                    if (isNormalBar) {
                        this.DrawAddonInfo(i == 0 ? "_ActionBar" : $"_ActionBar{i:00}");
                    }

                    ImGui.EndTabItem();
                }
            }

            // Pet hotbar is a special case
            if (ImGui.BeginTabItem("Pet##hotbarex")) {
                var petBar = isNormalBar ? &hotbarModule->PetHotbar : &hotbarModule->PetCrossHotbar;
                DrawHotbar(petBar);

                if (isNormalBar) {
                    this.DrawAddonInfo("_ActionBarEx");
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawHotbar(RaptureHotbarModule.Hotbar* hotbar) {
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
                DebugManager.ClickToCopyText($"{i + 1:00}", $"{(ulong)slot:X}");
                ImGui.SameLine();
                ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeight() * 4));
                ImGui.TableNextColumn();
                ImGui.Text("Empty");
                ImGui.PopStyleColor();
                continue;
            }

            var adjustedId = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
            DebugManager.ClickToCopyText($"{i + 1:00}", $"{(ulong)slot:X}");
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

            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(slot->IconId % 1000000, slot->IconId >= 1000000)).GetWrapOrDefault();
            if (icon != null) {
                ImGui.Image(icon.ImGuiHandle, new Vector2(32));
            } else {
                ImGui.GetWindowDrawList().AddRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(32), 0xFF0000FF, 4);
                ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos(), 0xFFFFFFFF, $"{slot->IconId}");

                ImGui.Dummy(new Vector2(32));
            }

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
                   
                    if (!Service.Data.Excel.GetSheet<Action>()!.TryGetRow(adjustedId, out var action)) {
                        ImGui.TextDisabled("Not Found");
                        break;
                    }

                    cooldownGroup = action.CooldownGroup;
                    break;
                }
                case RaptureHotbarModule.HotbarSlotType.Item: {
                    
                    if (!Service.Data.Excel.GetSheet<Item>()!.TryGetRow(slot->CommandId, out _)) {
                        ImGui.TextDisabled("Not Found");
                        break;
                    }

                    var cdg = ActionManager.Instance()->GetRecastGroup(2, slot->CommandId);
                    if (cdg < 81) cooldownGroup = cdg + 1;

                    break;
                }
                case RaptureHotbarModule.HotbarSlotType.GeneralAction: {
                     if (!Service.Data.Excel.GetSheet<GeneralAction>()!.TryGetRow(adjustedId, out _)) {
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
                ImGui.Text(cooldown != null ? $"{cooldown->IsActive} / {cooldown->Elapsed} / {cooldown->Total}" : "Failed");
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
            // UIDebug.DrawUnitBase(&addon->AtkUnitBase);
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
                if (!Service.Data.Excel.GetSheet<Action>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Item: {
                if (!Service.Data.Excel.GetSheet<Item>().TryGetRow(commandId % 500000, out var item)) return false;
                resolvedName = item.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.CraftAction: {
                if (!Service.Data.Excel.GetSheet<CraftAction>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.GeneralAction: {
                if (!Service.Data.Excel.GetSheet<GeneralAction>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.MainCommand: {
                if (!Service.Data.Excel.GetSheet<MainCommand>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.ExtraCommand: {
                if (!Service.Data.Excel.GetSheet<ExtraCommand>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.GearSet: {
                var gearsetModule = RaptureGearsetModule.Instance();
                var gearset = gearsetModule->GetGearset((int)commandId);

                if (gearset == null) {
                    resolvedName = $"InvalidGearset#{commandId}";
                    return false;
                }

                resolvedName = gearset->NameString;
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
                if (!Service.Data.Excel.GetSheet<Emote>().TryGetRow(commandId, out var emote)) return false;
                resolvedName = emote.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.EventItem: {
                if (!Service.Data.Excel.GetSheet<EventItem>().TryGetRow(commandId, out var item)) return false;
                resolvedName = item.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Mount: {
                if (!Service.Data.Excel.GetSheet<Mount>().TryGetRow(commandId, out var mount)) return false;
                resolvedName = mount.Singular.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.Companion: {
                if (!Service.Data.Excel.GetSheet<Companion>().TryGetRow(commandId, out var companion)) return false;
                resolvedName = companion.Singular.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.McGuffin: {
                if (!Service.Data.Excel.GetSheet<McGuffin>().TryGetRow(commandId, out var mcGuffin)) return false;
                resolvedName = mcGuffin.UIData.Value.Name.ExtractText();
                return true;
            }

            case RaptureHotbarModule.HotbarSlotType.PetAction: {
                if (!Service.Data.Excel.GetSheet<PetAction>().TryGetRow(commandId, out var action)) return false;
                resolvedName = action.Name.ExtractText();
                return true;
            }

            default: {
                resolvedName = $"Not Yet Supported ({type})";
                return false;
            }
        }
    }
}
