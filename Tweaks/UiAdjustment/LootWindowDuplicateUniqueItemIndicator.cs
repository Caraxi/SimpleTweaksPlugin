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
        base.Enable();
    }

    public override void Disable()
    {
        needGreedOnRequestedUpdateHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        needGreedOnRequestedUpdateHook?.Dispose();
        base.Dispose();
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

                switch (itemData)
                {
                    // Item is unique, and isn't consumable, just check quantity
                    case { IsUnique: true, ItemAction.Row: 0 } when GetItemCount(itemInfo.ItemId) > 0:
                        
                    // Item has a unlock action, 1 means item has been unlocked
                    case { ItemAction.Row: not 0 } when UIState.Instance()->IsItemActionUnlocked(ExdModule.GetItemRowById(itemInfo.ItemId)) is 1:
                        
                        var targetListItemId = listItemIndexArray[index];
                        var targetListItemNode = Common.GetNodeByID<AtkComponentNode>(&listComponentNode->Component->UldManager, (uint) targetListItemId);
                        if (targetListItemNode is not null && targetListItemNode->Component is not null)
                        {
                            // Here we modify the node to indicate that the player already has this item
                            // I'm not convinced that coloring it red is the best choice
                            // If you or anyone else has a better method to indicate that this item is not rollable, modify the code below
                            var imageNode = Common.GetNodeByID<AtkImageNode>(&targetListItemNode->Component->UldManager, 11);

                            imageNode->AtkResNode.AddRed = 0x55;
                        }
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