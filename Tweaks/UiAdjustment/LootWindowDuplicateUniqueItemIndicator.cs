#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Enhanced Loot Window")]
[TweakDescription("Marks unobtainable and already unlocked items in the loot window.")]
[TweakAuthor("MidoriKami")]
[TweakVersion(2)]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.2.1")]
[Changelog("1.8.3.0", "Rebuilt tweak to use images.")]
[Changelog("1.8.3.0", "Fixed tweak not checking armory and equipped items.")]
[Changelog("1.8.3.0", "Added 'Lock Loot Window' feature.")]
[Changelog("1.8.6.0", "Removed Window Lock Feature, 'Lock Window Position' tweak has returned.")]
public unsafe class LootWindowDuplicateUniqueItemIndicator : UiAdjustments.SubTweak {
    private readonly int[] listItemNodeIdArray = Enumerable.Range(21001, 31).Prepend(2).ToArray();

    private const int MinionCategory = 81;
    private const int MountCategory = 63;
    private const int MountSubCategory = 175;

    private enum ItemStatus {
        Unobtainable,
        AlreadyUnlocked,
        Normal
    }
    
    private class Config : TweakConfig {
        [TweakConfigOption("Mark Un-obtainable Items")]
        public bool MarkUnobtainable = true;

        [TweakConfigOption("Mark Already Unlocked Items")]
        public bool MarkAlreadyObtained = true;
    }

    private Config TweakConfig { get; set; } = null!;

    [AddonPostSetup("NeedGreed")]
    private void OnAddonSetup(AtkUnitBase* addon) {
        var listComponentNode = (AtkComponentNode*) addon->GetNodeById(6);
        if (listComponentNode is null || listComponentNode->Component is null) return;
        
        foreach (var index in listItemNodeIdArray) {
            var componentUldManager = &listComponentNode->Component->UldManager;
                    
            var lootItemNode = Common.GetNodeByID<AtkComponentNode>(componentUldManager, (uint)index);
            if (lootItemNode is null) continue;
                
            var crossNode = Common.GetNodeByID(componentUldManager, CustomNodes.Get(this, "CrossImage", index));
            if (crossNode is null) {
                MakeCrossNode(CustomNodes.Get(this, "CrossImage", index), lootItemNode);
            }
                        
            var padlockNode = Common.GetNodeByID(componentUldManager, CustomNodes.Get(this, "PadlockImage", index));
            if (padlockNode is null) {
                MakePadlockNode(CustomNodes.Get(this, "PadlockImage", index), lootItemNode);
            }
        }
    }
    
    [AddonFinalize("NeedGreed")]
    private void OnAddonFinalize(AtkUnitBase* addon) {
        var listComponentNode = (AtkComponentNode*) addon->GetNodeById(6);
        if (listComponentNode is null || listComponentNode->Component is null) return;
        
        foreach (var index in listItemNodeIdArray) {
            var componentUldManager = &listComponentNode->Component->UldManager;
                    
            var lootItemNode = Common.GetNodeByID<AtkComponentNode>(componentUldManager, (uint)index);
            if (lootItemNode is null) continue;

            var lootItemUldManager = &lootItemNode->Component->UldManager;

            var crossNode = Common.GetNodeByID<AtkImageNode>(lootItemUldManager, CustomNodes.Get(this, "CrossImage", index));
            if (crossNode is not null) {
                UiHelper.UnlinkAndFreeImageNode(crossNode, addon);
            }
                        
            var padlockNode = Common.GetNodeByID<AtkImageNode>(lootItemUldManager, CustomNodes.Get(this, "PadlockImage", index));
            if (padlockNode is not null) {
                UiHelper.UnlinkAndFreeImageNode(padlockNode, addon);
            }
        }
    }

    [AddonPostRequestedUpdate("NeedGreed")]
    private void OnNeedGreedRequestedUpdate(AddonNeedGreed* callingAddon) {
        try {
            var listComponentNode = (AtkComponentNode*) callingAddon->AtkUnitBase.GetNodeById(6);
            if (listComponentNode is null || listComponentNode->Component is null) return;
            
            // For each possible item slot, get the item info
            foreach (var index in Enumerable.Range(0, callingAddon->Items.Length)) {
                // If this data slot doesn't have an item id, skip.
                var itemInfo = callingAddon->Items[index];
                if (itemInfo.ItemId is 0) continue;

                var adjustedItemId = itemInfo.ItemId > 1_000_000 ? itemInfo.ItemId - 1_000_000 : itemInfo.ItemId;
                
                // If we can't match the item in lumina, skip.
                var itemData = Service.Data.GetExcelSheet<Item>()!.GetRow(adjustedItemId);
                if (itemData is null) continue;

                // If we can't get the ui node, skip
                var listItemNodeId = listItemNodeIdArray[index];
                var listItemNode = Common.GetNodeByID<AtkComponentNode>(&listComponentNode->Component->UldManager, (uint) listItemNodeId);
                if (listItemNode is null || listItemNode->Component is null) continue;
                
                switch (itemData) {
                    // Item is unique, and has no unlock action, and is unobtainable if we have any in our inventory
                    case { IsUnique: true, ItemAction.Row: 0 } when PlayerHasItem(itemInfo.ItemId):
                        
                    // Item is unobtainable if its a minion/mount and already unlocked
                    case { ItemUICategory.Row: MinionCategory } when IsItemAlreadyUnlocked(itemInfo.ItemId):
                    case { ItemUICategory.Row: MountCategory, ItemSortCategory.Row: MountSubCategory } when IsItemAlreadyUnlocked(itemInfo.ItemId):
                        UpdateNodeVisibility(listItemNode, listItemNodeId, ItemStatus.Unobtainable);
                        break;

                    // Item can be obtained if unlocked
                    case not null when IsItemAlreadyUnlocked(itemInfo.ItemId):
                        UpdateNodeVisibility(listItemNode, listItemNodeId, ItemStatus.AlreadyUnlocked);
                        break;
                    
                    // Item can be obtained normally
                    default:
                        UpdateNodeVisibility(listItemNode, listItemNodeId, ItemStatus.Normal);
                        break;
                }
            }
        }
        catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in LootWindowDuplicateUniqueItemIndicator, let MidoriKami know!");
        }
    }

    private void UpdateNodeVisibility(AtkComponentNode* listItemNode, int listItemId, ItemStatus status) {
        var crossNode = Common.GetNodeByID<AtkImageNode>(&listItemNode->Component->UldManager, CustomNodes.Get(this, "CrossImage", listItemId));
        var padlockNode = Common.GetNodeByID<AtkImageNode>(&listItemNode->Component->UldManager, CustomNodes.Get(this, "PadlockImage", listItemId));
        
        if (crossNode is null || padlockNode is null) return;
        
        switch (status) {
            case ItemStatus.AlreadyUnlocked when TweakConfig.MarkAlreadyObtained:
                crossNode->AtkResNode.ToggleVisibility(false);
                padlockNode->AtkResNode.ToggleVisibility(true);
                break;
            
            case ItemStatus.Unobtainable when TweakConfig.MarkUnobtainable:
                crossNode->AtkResNode.ToggleVisibility(true);
                padlockNode->AtkResNode.ToggleVisibility(false);
                break;
            
            default:
            case ItemStatus.Normal:
                crossNode->AtkResNode.ToggleVisibility(false);
                padlockNode->AtkResNode.ToggleVisibility(false);
                break;
        }
    }

    private bool IsItemAlreadyUnlocked(uint itemId) {
        var exdItem = ExdModule.GetItemRowById(itemId);
        return exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1;
    }
    
    private void MakeCrossNode(uint NodeId, AtkComponentNode* parent) {
        var imageNode = UiHelper.MakeImageNode(NodeId, new UiHelper.PartInfo(0, 0, 32, 32));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents; // 8243;
        imageNode->WrapMode = 1;

        imageNode->LoadIconTexture(61502, 0);

        imageNode->AtkResNode.SetWidth(32);
        imageNode->AtkResNode.SetHeight(32);
        imageNode->AtkResNode.SetScale(1.25f, 1.25f);
        imageNode->AtkResNode.SetPositionShort(14, 14);
        
        imageNode->AtkResNode.ToggleVisibility(true);
        
        var targetTextNode = Common.GetNodeByID<AtkResNode>(&parent->Component->UldManager, 11);
        UiHelper.LinkNodeAfterTargetNode((AtkResNode*) imageNode, parent, targetTextNode);
    }

    private void MakePadlockNode(uint NodeId, AtkComponentNode* parent) {
        var imageNode = UiHelper.MakeImageNode(NodeId, new UiHelper.PartInfo(48, 0, 20, 24));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents; // 8243;
        imageNode->WrapMode = 1;

        imageNode->LoadTexture("ui/uld/ActionBar_hr1.tex");

        imageNode->AtkResNode.Color.A = 0xAA;

        imageNode->AtkResNode.SetWidth(20);
        imageNode->AtkResNode.SetHeight(24);
        imageNode->AtkResNode.SetPositionShort(22, 20); 
        
        imageNode->AtkResNode.ToggleVisibility(true);
        
        var targetTextNode = Common.GetNodeByID<AtkResNode>(&parent->Component->UldManager, 11);
        UiHelper.LinkNodeAfterTargetNode((AtkResNode*) imageNode, parent, targetTextNode);
    }

    private bool PlayerHasItem(uint itemId) {
        // Only check main inventories, don't include any special inventories
        var inventories = new List<InventoryType> {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
            
            InventoryType.EquippedItems,
            
            InventoryType.ArmoryMainHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryWaist,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,

            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryEar,
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings,
        };

        return inventories.Sum(inventory => InventoryManager.Instance()->GetItemCountInContainer(itemId, inventory)) > 0;
    }
}
