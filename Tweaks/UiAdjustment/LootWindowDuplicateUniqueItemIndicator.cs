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
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class LootWindowDuplicateUniqueItemIndicator : UiAdjustments.SubTweak
{
    public override string Name => "Enhanced Loot Window";
    protected override string Author => "MidoriKami";
    public override string Description => "Marks unlootable and already obtained items in the loot window.";

    private delegate nint OnRequestedUpdateDelegate(nint a1, nint a2, nint a3);
    private delegate nint MoveAddonDetour(RaptureAtkModule* atkModule, AtkUnitBase* addon, nint idk2);
    
    [Signature("40 53 48 83 EC 20 48 8B 42 58", DetourName = nameof(OnNeedGreedRequestedUpdate))]
    private readonly Hook<OnRequestedUpdateDelegate>? needGreedOnRequestedUpdateHook = null!;

    [Signature("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", DetourName = nameof(OnNeedGreedMove))]
    private readonly Hook<MoveAddonDetour>? needGreedOnMoveHook = null!;

    private static AtkUnitBase* AddonNeedGreed => (AtkUnitBase*) Service.GameGui.GetAddonByName("NeedGreed");

    private const uint CrossBaseId = 1000U;
    private const uint PadlockBaseId = 2000U;
    
    public class Config : TweakConfig
    {
        [TweakConfigOption("Lock Window Position")]
        public bool LockWindowPosition = false;

        [TweakConfigOption("Mark Un-obtainable Items")]
        public bool MarkUnobtainable = true;

        [TweakConfigOption("Mark Already Unlocked Items")]
        public bool MarkAlreadyObtained = true;
    }

    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.2.1");

        SignatureHelper.Initialise(this);
        Ready = true;
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        needGreedOnRequestedUpdateHook?.Enable();
        needGreedOnMoveHook?.Enable();
        Common.AddonSetup += OnAddonSetup;
        Common.AddonFinalize += OnAddonFinalize;
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        
        needGreedOnRequestedUpdateHook?.Disable();
        needGreedOnMoveHook?.Disable();
        Common.AddonSetup -= OnAddonSetup;
        Common.AddonFinalize -= OnAddonFinalize;
        base.Disable();
    }

    public override void Dispose()
    {
        needGreedOnRequestedUpdateHook?.Dispose();
        needGreedOnMoveHook?.Dispose();
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
                MakeCrossNode(CrossBaseId + index, lootItemNode);
            }
                        
            var padlockNode = Common.GetNodeByID(componentUldManager, PadlockBaseId + index);
            if (padlockNode is null)
            {
                MakePadlockNode(PadlockBaseId + index, lootItemNode);
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

                // Get item datasheet pointer now, so we can check it for null before checking itemActionUnlocked
                var exdItem = ExdModule.GetItemRowById(itemInfo.ItemId);
                
                switch (itemData)
                {
                    // Item is unique, and has no unlock action, and is unobtainable if we have any in our inventory
                    case { IsUnique: true, ItemAction.Row: 0 } when GetItemCount(itemInfo.ItemId) > 0 && TweakConfig.MarkUnobtainable:
                        
                    // Item is unobtainable if unlocked
                    // Category 81: Minion
                    // Category 63: Mount when ItemSortCategory is 175
                    // Category 63: Chocobo Barding when ItemSortCategory is 130
                    case { ItemUICategory.Row: 81 } when exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1 && TweakConfig.MarkUnobtainable:
                    case { ItemUICategory.Row: 63, ItemSortCategory.Row: 175 } when exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1 && TweakConfig.MarkUnobtainable:
                        crossNode->AtkResNode.ToggleVisibility(true);
                        padlockNode->AtkResNode.ToggleVisibility(false);
                        break;

                    // Item can be obtained if unlocked
                    case { } when exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1 && TweakConfig.MarkAlreadyObtained:
                        crossNode->AtkResNode.ToggleVisibility(false);
                        padlockNode->AtkResNode.ToggleVisibility(true);
                        break;
                    
                    // Item can be obtained normally
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

    private nint OnNeedGreedMove(RaptureAtkModule* atkModule, AtkUnitBase* addon, nint idk2)
    {
        var skipOriginal = false;
        
        try
        {
            skipOriginal = TweakConfig.LockWindowPosition && addon == AddonNeedGreed;
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in LootWindowDuplicateUniqueItemIndicator, let MidoriKami know!");
        }

        return skipOriginal ? nint.Zero : needGreedOnMoveHook!.Original(atkModule, addon, idk2);
    }
    
    private void MakeCrossNode(uint nodeId, AtkComponentNode* parent)
    {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 32, 32));
        imageNode->AtkResNode.Flags = 8243;
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

    private void MakePadlockNode(uint nodeId, AtkComponentNode* parent)
    {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(48, 0, 20, 24));
        imageNode->AtkResNode.Flags = 8243;
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