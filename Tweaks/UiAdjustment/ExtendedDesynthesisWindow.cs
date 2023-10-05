using System;
using Dalamud.Game.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Extended Desynthesis Window")]
[TweakDescription("Shows your current desynthesis level and the item's optimal level on the desynthesis item selection window.\nAlso indicates if an item is part of a gear set, optionally preventing selection of gearset items.")]
[TweakVersion(2)]
public unsafe class ExtendedDesynthesisWindow : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool BlockClickOnGearset;
        public bool YellowForSkillGain = true;
        public bool Delta;

        public bool ShowAll;
        public bool ShowAllExcludeNoSkill;
        public bool ShowAllExcludeGearset;
        public bool ShowAllDefault;
        public bool ShowAllExcludeArmoury;
        public int[]? ShowAllSorting;
    }

    private static readonly ByteColor Red = new() { A = 0xFF, R = 0xEE, G = 0x44, B = 0x44 };
    private static readonly ByteColor Green = new() { A = 0xFF, R = 0x00, G = 0xCC, B = 0x00 };
    private static readonly ByteColor Yellow = new() { A = 0xFF, R = 0xCC, G = 0xCC, B = 0x00 };

    public Configs Config { get; private set; } = new();

    private uint maxDesynthLevel;
    
    private void DrawConfig() {
        ImGui.Checkbox(LocString("BlockClickOnGearset", "Block clicking on gearset items."), ref Config.BlockClickOnGearset);
        ImGui.Checkbox(LocString("YellowForSkillGain", "Highlight potential skill gains (Yellow)"), ref Config.YellowForSkillGain);
        ImGui.Checkbox(LocString("DesynthesisDelta", "Show desynthesis delta"), ref Config.Delta);

        ImGui.TextDisabled("The 'Show All Items' option is currently removed.");
    }
    
    private delegate nint UpdateItemDelegate(nint a1, uint index, nint a3, AtkComponentBase* listItemRenderer);

    [TweakHook, Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B 38", DetourName = nameof(UpdateItemDetour))]
    private readonly HookWrapper<UpdateItemDelegate>? updateItemHook = null!;

    private nint UpdateItemDetour(nint a1, uint index, nint a3, AtkComponentBase* listItemRenderer) {
        try {
            Update(listItemRenderer, index);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Error handling UpdateItem");
        }

        return updateItemHook!.Original(a1, index, a3, listItemRenderer);
    }

    private void Update(AtkComponentBase* listItemRenderer, uint index) {
        var agent = AgentSalvage.Instance();
        if (agent->ItemCount <= index) return;

        var itemEntry = agent->ItemList + index;
        var inventoryItem = InventoryManager.Instance()->GetInventoryContainer(itemEntry->InventoryType)->GetInventorySlot((int)itemEntry->InventorySlot);

        var itemData = Service.Data.GetExcelSheet<Item>()?.GetRow(inventoryItem->ItemID);
        if (itemData == null) return;
        
        var skillText = (AtkTextNode*)Common.GetNodeByID(listItemRenderer, CustomNodes.Get(this, "Skill"), NodeType.Text);
        var gearSetText = (AtkTextNode*)Common.GetNodeByID(listItemRenderer, CustomNodes.Get(this, "GearSet"), NodeType.Text);

        if (skillText == null || gearSetText == null) return;

        
        
        var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(itemData.ClassJobRepair.Row);

        ByteColor c;

        if (desynthLevel >= maxDesynthLevel) {
            c = Green;
        } else {
            if (desynthLevel > itemData.LevelItem.Row) {
                if (Config.YellowForSkillGain && desynthLevel < itemData.LevelItem.Row + 50) {
                    c = Yellow;
                } else {
                    c = Green;
                }
            } else {
                c = Red;
            }
        }
                
        skillText->TextColor = c;

        if (Config.Delta) {
            var desynthDelta = itemData.LevelItem.Row - desynthLevel;
            skillText->SetText($"{itemData.LevelItem.Row} ({desynthDelta:+#;-#})");
        } else {
            skillText->SetText($"{desynthLevel:F0}/{itemData.LevelItem.Row}");
        }
        
        
        gearSetText->SetText(string.Empty);
        listItemRenderer->UldManager.RootNode->ToggleVisibility(true);
        if (GetGearSetWithItem(inventoryItem) != null) {
            gearSetText->SetText($"{(char) SeIconChar.BoxedStar}");
            if (Config.BlockClickOnGearset) {
                listItemRenderer->UldManager.RootNode->ToggleVisibility(false);
            }
        }
        
    }

    private static RaptureGearsetModule.GearsetEntry* GetGearSetWithItem(InventoryItem* slot) {
        var gearSetModule = RaptureGearsetModule.Instance();
        var itemIdWithHQ = slot->ItemID;
        if ((slot->Flags & InventoryItem.ItemFlags.HQ) > 0) itemIdWithHQ += 1000000;
        for (var gs = 0; gs < 101; gs++) {
            var gearSet = gearSetModule->GetGearset(gs);
            if (gearSet == null) continue;
            if (gearSet->ID != gs) break;
            if (!gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            foreach (var i in gearSet->ItemsSpan) {
                if (i.ItemID == itemIdWithHQ) {
                    return gearSet;
                }
            }
        }

        return null;
    }

    [AddonPostSetup("SalvageItemSelector")]
    public void SalvageItemSelectorSetup(AtkUnitBase* unitBase) {
        void AddHeader(string label, uint id, ushort size) {
            var headerContainer = unitBase->GetNodeById(7);
            var separator = unitBase->GetNodeById(11);
            if (headerContainer == null) return;
            if (separator == null) return;

            var lastNode = headerContainer->ChildNode;
            if (lastNode == null) return;
            var n = lastNode;
            while (n != null) {
                if (n->Type != NodeType.Text) continue;
                if (n->GetX() > lastNode->GetX()) {
                    lastNode = n;
                }

                n = n->PrevSiblingNode;
            }

            var lastLabel = lastNode->GetAsAtkTextNode();
            if (lastLabel == null) return;

            var header = UiHelper.MakeTextNode(id);
            header->SetText(label);
            header->AtkResNode.SetWidth(size);
            header->AtkResNode.SetHeight(lastNode->GetHeight());
            header->AtkResNode.SetX(lastNode->GetX() + lastNode->GetWidth());
            header->AtkResNode.SetY(lastNode->GetY());
            header->SetFont(lastLabel->FontType);
            header->SetAlignment(lastLabel->AlignmentType);
            header->LineSpacing = lastLabel->LineSpacing;
            header->TextColor = lastLabel->TextColor;
            header->EdgeColor = lastLabel->EdgeColor;
            header->BackgroundColor = lastLabel->BackgroundColor;
            header->TextFlags = lastLabel->TextFlags;
            header->TextFlags2 = lastLabel->TextFlags2;

            header->AtkResNode.DrawFlags = lastNode->DrawFlags;
            header->AtkResNode.NodeFlags = lastNode->NodeFlags;
            header->AtkResNode.ToggleVisibility(true);
            header->FontSize = lastLabel->FontSize;

            UiHelper.LinkNodeAfterTargetNode(&header->AtkResNode, unitBase, lastNode);

            headerContainer->SetWidth((ushort)(header->AtkResNode.GetX() + header->AtkResNode.GetWidth()));
            separator->SetWidth((ushort)(header->AtkResNode.GetX() + header->AtkResNode.GetWidth() + 6));

            // Resize Window
            unitBase->RootNode->SetWidth((ushort)(unitBase->RootNode->GetWidth() + size));

            var windowNode = (AtkComponentNode*)unitBase->GetNodeById(14);
            if (windowNode == null) return;
            windowNode->AtkResNode.SetWidth((ushort)(windowNode->AtkResNode.GetWidth() + size));

            var component = windowNode->Component;
            if (component == null) return;

            foreach (var resizeId in new uint[] { 2, 8, 9, 10, 11, 12, 13 }) {
                var resizeNode = component->UldManager.SearchNodeById(resizeId);
                if (resizeNode == null) continue;
                resizeNode->SetWidth((ushort)(resizeNode->GetWidth() + size));
            }

            foreach (var moveId in new uint[] { 5, 6, 7 }) {
                var moveNode = component->UldManager.SearchNodeById(moveId);
                if (moveNode == null) continue;
                moveNode->SetX(moveNode->GetX() + size);
            }

            // Resize List
            var listNode = (AtkComponentNode*)unitBase->GetNodeById(12);
            if (listNode == null) return;
            listNode->AtkResNode.SetWidth((ushort)(listNode->AtkResNode.GetWidth() + size));

            foreach (var node in Common.GetNodeList(listNode->Component)) {
                if (node->NodeID == 5) {
                    node->SetX(node->GetX() + size);
                    continue;
                }

                node->SetWidth((ushort)(node->GetWidth() + size));

                if (node->NodeID == 4 || (node->NodeID > 41000 && node->NodeID < 41100)) {
                    var listItemRendererNode = (AtkComponentNode*)node;
                    var listItemRenderer = listItemRendererNode->Component;

                    var baseNode = listItemRenderer->GetTextNodeById(6);
                    var baseTextNode = (AtkTextNode*)baseNode;
                    var entryNode = UiHelper.MakeTextNode(id);

                    entryNode->SetText(string.Empty);
                    entryNode->AtkResNode.SetWidth(size);
                    entryNode->AtkResNode.SetHeight(baseNode->GetHeight());
                    entryNode->AtkResNode.SetX(lastNode->GetX() + lastNode->GetWidth());
                    entryNode->AtkResNode.SetY(baseNode->GetY());
                    entryNode->SetFont(baseTextNode->FontType);
                    entryNode->SetAlignment(AlignmentType.Center);
                    entryNode->LineSpacing = baseTextNode->LineSpacing;
                    entryNode->TextColor = baseTextNode->TextColor;
                    entryNode->EdgeColor = baseTextNode->EdgeColor;
                    entryNode->BackgroundColor = baseTextNode->BackgroundColor;
                    entryNode->TextFlags = baseTextNode->TextFlags;
                    entryNode->TextFlags2 = baseTextNode->TextFlags2;
                    entryNode->AtkResNode.DrawFlags = baseNode->DrawFlags;
                    entryNode->AtkResNode.NodeFlags = baseNode->NodeFlags;
                    entryNode->AtkResNode.ToggleVisibility(true);
                    entryNode->FontSize = baseTextNode->FontSize;

                    UiHelper.LinkNodeAfterTargetNode(&entryNode->AtkResNode, listItemRendererNode, listItemRenderer->UldManager.RootNode);
                }
            }
        }

        AddHeader("Skill", CustomNodes.Get(this, "Skill"), 120);
        AddHeader("Gear\nSet", CustomNodes.Get(this, "GearSet"), 60);
    }

    protected override void Enable() {
        if (maxDesynthLevel == 0) {
            foreach (var i in Service.Data.Excel.GetSheet<Item>()!) {
                if (i.Desynth > 0 && i.LevelItem.Row > maxDesynthLevel) maxDesynthLevel = i.LevelItem.Row;
            }
        }
        
        Config = LoadConfig<Configs>() ?? new Configs();
    }

    protected override void Disable() {
        SaveConfig(Config);
        Common.CloseAddon("SalvageItemSelector");
    }
}
