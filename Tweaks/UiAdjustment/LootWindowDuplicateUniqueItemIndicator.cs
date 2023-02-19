#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class LootWindowDuplicateUniqueItemIndicator : UiAdjustments.SubTweak
{
    public override string Name => "Loot Window Duplicate Unique Item Indicator";
    protected override string Author => "MidoriKami";
    public override string Description => "Recolors unique items that you already have in the loot window.";

    private delegate nint OnRequestedUpdateDelegate(nint a1, nint a2, nint a3);

    [Signature("40 53 48 83 EC 20 48 8B 42 58", DetourName = nameof(OnNeedGreedRequestedUpdate))]
    private readonly Hook<OnRequestedUpdateDelegate>? needGreedOnRequestedUpdateHook = null!;

    private static AtkUnitBase* AddonNeedGreed => (AtkUnitBase*) Service.GameGui.GetAddonByName("NeedGreed");

    private const uint CrossBaseId = 1000U;
    private const uint PadlockBaseId = 2000U;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.2.1");

        SignatureHelper.Initialise(this);
        Ready = true;
    }

    public override void Enable()
    {
        needGreedOnRequestedUpdateHook?.Enable();
        Common.AddonSetup += OnAddonSetup;
        Common.AddonFinalize += OnAddonFinalize;
        base.Enable();
    }

    public override void Disable()
    {
        needGreedOnRequestedUpdateHook?.Disable();
        Common.AddonSetup -= OnAddonSetup;
        Common.AddonFinalize -= OnAddonFinalize;
        base.Disable();
    }

    public override void Dispose()
    {
        needGreedOnRequestedUpdateHook?.Dispose();
        Common.AddonSetup -= OnAddonSetup;
        Common.AddonFinalize -= OnAddonFinalize;
        base.Dispose();
    }

    private void OnAddonSetup(SetupAddonArgs obj)
    {
        if (obj.AddonName != "NeedGreed") return;
        
        var listComponentNode = (AtkComponentNode*) obj.Addon->GetNodeById(6);
        if (listComponentNode is null || listComponentNode->Component is null) return;
        
        foreach (uint index in Enumerable.Range(21001, 31).Prepend(2).ToArray())
        {
            var componentUldManager = &listComponentNode->Component->UldManager;
                    
            var lootItemNode = Common.GetNodeByID<AtkComponentNode>(componentUldManager, index);
            if (lootItemNode is null) continue;
                
            var crossNode = Common.GetNodeByID(componentUldManager, CrossBaseId + index);
            if (crossNode is null)
            {
                MakeCrossNode(CrossBaseId + index, (short) lootItemNode->AtkResNode.Y, (short) lootItemNode->AtkResNode.Y, lootItemNode);
            }
                        
            var padlockNode = Common.GetNodeByID(componentUldManager, PadlockBaseId + index);
            if (padlockNode is null)
            {
                MakePadlockNode(PadlockBaseId + index, (short) lootItemNode->AtkResNode.Y, (short) lootItemNode->AtkResNode.Y, lootItemNode);
            }
        }
    }
    
    private void OnAddonFinalize(SetupAddonArgs obj)
    {
        if (obj.AddonName != "NeedGreed") return;
        
        var listComponentNode = (AtkComponentNode*) obj.Addon->GetNodeById(6);
        if (listComponentNode is not null && listComponentNode->Component is not null)
        {
            foreach (uint index in Enumerable.Range(21001, 31).Prepend(2).ToArray())
            {
                var componentUldManager = &listComponentNode->Component->UldManager;
                    
                var lootItemNode = Common.GetNodeByID<AtkComponentNode>(componentUldManager, index);
                if (lootItemNode is not null)
                {
                    var crossNode = Common.GetNodeByID<AtkImageNode>(componentUldManager, CrossBaseId + index);
                    if (crossNode is not null)
                    {
                        UiHelper.UnlinkAndFreeImageNode(crossNode, AddonNeedGreed);
                    }
                        
                    var padlockNode = Common.GetNodeByID<AtkImageNode>(componentUldManager, PadlockBaseId + index);
                    if (padlockNode is not null)
                    {
                        UiHelper.UnlinkAndFreeImageNode(padlockNode, AddonNeedGreed);
                    }
                }
            }
        }
    }

    private nint OnNeedGreedRequestedUpdate(nint addon, nint a2, nint a3)
    {
        var result = needGreedOnRequestedUpdateHook!.Original(addon, a2, a3);

        try
        {
            var callingAddon = (AtkUnitBase*) addon;

            var listComponentNode = (AtkComponentNode*) callingAddon->GetNodeById(6);
            if (listComponentNode is null) return result;
            if (listComponentNode->Component is null) return result;
            
            // Array of ListItemNode ID's, in display order
            var listItemIndexArray = Enumerable.Range(21001, 31).Prepend(2).ToArray();
            var lootItemInfoArray = (LootItemInfo*)(addon + 0x228);

            // For each possible item slot, get the item info
            foreach (var index in Enumerable.Range(0, 32))
            {
                // If this data slot doesn't have an item id, skip.
                var itemInfo = lootItemInfoArray[index];
                if (itemInfo.ItemId is 0) continue;

                // If we can't match the item in lumina, skip.
                var itemData = Service.Data.GetExcelSheet<Item>()!.GetRow(itemInfo.ItemId);
                if(itemData is null) continue;

                var targetListItemId = listItemIndexArray[index];
                var targetListItemNode = Common.GetNodeByID<AtkComponentNode>(&listComponentNode->Component->UldManager, (uint) targetListItemId);

                if (targetListItemNode is null || targetListItemNode->Component is null) continue;
                
                var crossNode = Common.GetNodeByID<AtkImageNode>(&targetListItemNode->Component->UldManager, CrossBaseId + (uint) targetListItemId);
                var padlockNode = Common.GetNodeByID<AtkImageNode>(&targetListItemNode->Component->UldManager, PadlockBaseId + (uint) targetListItemId);
                
                switch (itemData)
                {
                    // Item is unique, and isn't consumable, just check quantity
                    case { IsUnique: true, ItemAction.Row: 0 } when GetItemCount(itemInfo.ItemId) > 0:
                        crossNode->AtkResNode.ToggleVisibility(true);
                        padlockNode->AtkResNode.ToggleVisibility(false);
                        break;

                    // Item has a unlock action, 1 means item has been unlocked
                    case { } when UIState.Instance()->IsItemActionUnlocked(ExdModule.GetItemRowById(itemInfo.ItemId)) is 1:
                        crossNode->AtkResNode.ToggleVisibility(false);
                        padlockNode->AtkResNode.ToggleVisibility(true);
                        break;
                    
                    default:
                        crossNode->AtkResNode.ToggleVisibility(false);
                        padlockNode->AtkResNode.ToggleVisibility(false);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in LootWindowDuplicateUniqueItemIndicator, let MidoriKami know!");
        }

        return result;
    }
    
    private void MakeCrossNode(uint nodeId, short x, short y, AtkComponentNode* parent)
    {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 32, 32));
        imageNode->AtkResNode.Flags = 8243;

        imageNode->LoadIconTexture(61502, 1);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(32);
        imageNode->AtkResNode.SetHeight(32);
        imageNode->AtkResNode.SetScale(1.25f, 1.25f);
        imageNode->AtkResNode.SetPositionShort((short)(x + 14u), (short)(y + 14u));
        
        var targetTextNode = Common.GetNodeByID<AtkResNode>(&parent->Component->UldManager, 11);
        UiHelper.LinkNodeAfterTargetNode(imageNode, parent, targetTextNode);
    }

    private void MakePadlockNode(uint nodeId, short x, short y, AtkComponentNode* parent)
    {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(48, 0, 20, 24));
        imageNode->AtkResNode.Flags = 8243;
        imageNode->WrapMode = 1;

        imageNode->LoadTexture("ui/uld/ActionBar_hr1.tex");
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.Color.A = 0xAA;

        imageNode->AtkResNode.SetWidth(20);
        imageNode->AtkResNode.SetHeight(24);
        imageNode->AtkResNode.SetPositionShort((short)(x + 22u), (short)(y + 20u));
        
        var targetTextNode = Common.GetNodeByID<AtkResNode>(&parent->Component->UldManager, 11);
        UiHelper.LinkNodeAfterTargetNode(imageNode, parent, targetTextNode);
    }

    private int GetItemCount(uint itemId)
    {
        // Only check main inventories, don't include any special inventories
        var inventories = new List<InventoryType>
        {
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

        return inventories.Sum(inventory => InventoryManager.Instance()->GetItemCountInContainer(itemId, inventory));
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0x28)]
    private struct LootItemInfo
    {
        [FieldOffset(0x00)] public readonly uint ItemId;
    }
}