#nullable enable
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
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
                
                // If the item is not unique, skip.
                if (!itemData.IsUnique) continue;

                // If we have any of this unique item
                if (InventoryManager.Instance()->GetInventoryItemCount(itemData.RowId) > 0)
                {
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
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in LootWindowDuplicateUniqueItemIndicator, let MidoriKami know!");
        }

        return result;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0x28)]
    private struct LootItemInfo
    {
        [FieldOffset(0x00)] public readonly uint ItemId;
    }
}