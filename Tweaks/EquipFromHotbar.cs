using System;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Equip items from Hotbars")]
[TweakDescription("Enables the ability to equip items assigned to hotbars.")]
[TweakCategory(TweakCategory.UI)]
[TweakReleaseVersion("1.9.7.0")]
public unsafe class EquipFromHotbar : Tweak {
    private void DrawConfig() {
        using (ImRaii.PushIndent()) {
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped("Equipment will be searched for in the armoury chest and main inventory.");
        
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped("If multiple matching items are in your inventory, the first in inventory will be used.");
        
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped("When equipping a ring, hold the SHIFT key to equip to left finger.");
        
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped("Any equipment with a special action, such as the teleport with Eternity Ring, will not equip from hotbars.");
        }
        
    }
    
    private delegate void* UseItem(AgentInventoryContext* inventoryContext, uint a2, uint a3, uint a4, ushort a5);

    [TweakHook, Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 89 7C 24", DetourName = nameof(UseItemDetour))]
    private readonly HookWrapper<UseItem> useItemHook = null!;

    private delegate void MoveItem(RaptureAtkModule* a1, void* outValue, AtkValue* atkValues);
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 55 57 41 56 48 8B EC 48 83 EC 40 4C 8B F1")]
    private readonly MoveItem moveItem = null!;
    
    private uint GetContainerId(InventoryType inventoryType) => inventoryType switch {
        InventoryType.Inventory1 => 48,
        InventoryType.Inventory2 => 49,
        InventoryType.Inventory3 => 50,
        InventoryType.Inventory4 => 51,
        InventoryType.ArmoryMainHand => 57,
        InventoryType.ArmoryHead => 58,
        InventoryType.ArmoryBody => 59,
        InventoryType.ArmoryHands => 60,
        InventoryType.ArmoryLegs => 61,
        InventoryType.ArmoryFeets => 62,
        InventoryType.ArmoryOffHand => 63,
        InventoryType.ArmoryEar => 64,
        InventoryType.ArmoryNeck => 65,
        InventoryType.ArmoryWrist => 66,
        InventoryType.ArmoryRings => 67,
        InventoryType.ArmorySoulCrystal => 68,
        InventoryType.EquippedItems => 4,
        _ => 0
    };
    
    private void Equip(InventoryType sourceInventory, uint sourceSlot, uint equipSlot) {
        var sourceContainerId = GetContainerId(sourceInventory);
        var destinationContainerId = GetContainerId(InventoryType.EquippedItems);
        if (sourceContainerId != 0 && destinationContainerId != 0) {
            var eis = stackalloc AtkValue[4];
            var dropOut = stackalloc byte[32];
            for (var i = 0; i < 4; i++) eis[i].Type = ValueType.UInt;
            eis[0].UInt = sourceContainerId;
            eis[1].UInt = sourceSlot;
            eis[2].UInt = destinationContainerId;
            eis[3].UInt = equipSlot;
            var atkModule = RaptureAtkModule.Instance();
            moveItem(atkModule, dropOut, eis);
        }
    }
    
    private void* UseItemDetour(AgentInventoryContext* inventoryContext, uint itemId, uint a3, uint a4, ushort a5) {
        var retVal = useItemHook.Original(inventoryContext, itemId, a3, a4, a5);
        try {
            if (!(a3 == 9999 && a4 == 0 && a5 == 0 && itemId < 1500000)) return retVal;
            var isHq = itemId > 1000000;
            var realId = itemId % 500000;
            var item = Service.Data.GetExcelSheet<Item>()?.GetRow(realId);
            if (item == null) return retVal;
            if (item.EquipSlotCategory.Row == 0 || item.EquipSlotCategory.Value == null) return retVal;
            if (item.ItemAction.Row != 0)return retVal;
            
            var itemOrderModule = ItemOrderModule.Instance();
            var esc = item.EquipSlotCategory.Value;
            if (esc.MainHand == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryMainHand, 0, itemOrderModule->ArmouryMainHandSorter)) return retVal;
            if (esc.OffHand == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryOffHand, 1, itemOrderModule->ArmouryOffHandSorter)) return retVal;
            if (esc.Head == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryHead, 2, itemOrderModule->ArmouryHeadSorter)) return retVal;
            if (esc.Body == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryBody, 3, itemOrderModule->ArmouryBodySorter)) return retVal;
            if (esc.Gloves == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryHands, 4, itemOrderModule->ArmouryHandsSorter)) return retVal;
            if (esc.Legs == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryLegs, 5, itemOrderModule->ArmouryLegsSorter)) return retVal;
            if (esc.Feet == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryFeets, 6, itemOrderModule->ArmouryFeetSorter)) return retVal;
            if (esc.Ears == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryEar, 7, itemOrderModule->ArmouryEarsSorter)) return retVal;
            if (esc.Neck == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryNeck, 8, itemOrderModule->ArmouryNeckSorter)) return retVal;
            if (esc.Wrists == 1 && FindAndEquip(realId, isHq, InventoryType.ArmoryWrist, 9, itemOrderModule->ArmouryWristsSorter)) return retVal;
            if ((esc.FingerL == 1 || esc.FingerR == 1) && FindAndEquip(realId, isHq, InventoryType.ArmoryRings, Service.KeyState[VirtualKey.SHIFT] ? 11U : 10U, itemOrderModule->ArmouryRingsSorter)) return retVal;
            if (esc.SoulCrystal == 1 && FindAndEquip(realId, isHq, InventoryType.ArmorySoulCrystal, 12, itemOrderModule->ArmourySoulCrystalSorter)) return retVal;
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Error in UseItemDetour");
            return retVal;
        }
        
        return retVal;
    }
    
    private bool FindAndEquip(uint itemId, bool isHq, InventoryType inventoryType, uint equipSlot, ItemOrderModuleSorter* sorter) {
        var inventoryManager = InventoryManager.Instance();
        for (var i = 0U; i < sorter->Items.Size(); i++) {
            var entry = sorter->Items.Get(i).Value;
            var item = inventoryManager->GetInventorySlot(inventoryType + entry->Page, entry->Slot);
            if (item->ItemId == itemId && item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) == isHq) {
                var page = (uint) (i / sorter->ItemsPerPage);
                var slot = (uint) (i % sorter->ItemsPerPage);
                Equip(inventoryType + page, slot, equipSlot);
                return true;
            }
        }
        
        if (inventoryType != InventoryType.Inventory1) return FindAndEquip(itemId, isHq, InventoryType.Inventory1, equipSlot, ItemOrderModule.Instance()->InventorySorter);
        return false;
    }
}
