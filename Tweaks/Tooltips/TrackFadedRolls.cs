using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public unsafe class TrackFadedRolls : TooltipTweaks.SubTweak {
    // Thank you to ascclemens for the inspiration to do this through GoodMemory, as well as the base model for how to
    // get the crafted orchestrion rolls from the faded ones.
    
    public override string Name => "Track Faded Orchestrion Rolls";
    protected override string Author => "KazWolfe";
    public override string Description => "Adds the collected checkmark to Faded Orchestrion Rolls.";
    
    private delegate byte IsItemActionUnlocked(UIState* uiState, IntPtr item);
    private HookWrapper<IsItemActionUnlocked>? _isItemActionUnlockedHookWrapper;

    public override void Enable() {
        this._isItemActionUnlockedHookWrapper ??=
            Common.Hook<IsItemActionUnlocked>(UIState.Addresses.IsItemActionUnlocked.Value, this.IsItemActionUnlockedDetour);
        this._isItemActionUnlockedHookWrapper?.Enable();

        base.Enable();
    }

    public override void Disable() {
        this._isItemActionUnlockedHookWrapper?.Disable();
        
        base.Disable();
    }

    public override void Dispose() {
        this._isItemActionUnlockedHookWrapper?.Dispose();

        base.Dispose();
    }
    
    private byte IsItemActionUnlockedDetour(UIState* uiState, IntPtr item) {
        // item.FilterGroup == 12, Item.ItemUICategory == 94, Item.ItemLevel == 1
        if (!(*(byte*) (item + 0x97) == 12 && *(byte*) (item + 0x98) == 94 && *(byte*) (item + 0x8A) == 1)) {
            return this._isItemActionUnlockedHookWrapper!.Original(uiState, item);
        }

        // Why is this such a mess? Because the Item row from EXD does not contain the item ID of the item.
        // So, what are we to do? Abuse Lumina. This is suboptimal. I do not care. I just want this done.
        var itemName = Marshal.PtrToStringUTF8(item + *(ushort*) item);
        if (itemName == null) return 4;

        var luminaItem = GetRollBySingularName(itemName);
        if (luminaItem == null) return 4;
        
        var areAllUnlocked = GetRollsCraftedWithItem(luminaItem)
            .TrueForAll(o => UIState.Instance()->PlayerState.IsOrchestrionRollUnlocked(o.AdditionalData));

        return areAllUnlocked ? (byte) 1 : (byte) 2;
    }

    private static Item? GetRollBySingularName(string singular) {
        return Service.Data.Excel.GetSheet<Item>()!
            .Where(item => (item.FilterGroup == 12 && item.ItemUICategory.Row == 94))
            .FirstOrDefault(item => item.Singular.RawString.Equals(singular));
    }

    private static List<Item> GetRollsCraftedWithItem(Item item) {
        return Service.Data.Excel.GetSheet<Recipe>()!
            .Where(r => r.UnkData5.Any(i => i.ItemIngredient == item.RowId))
            .Select(r => r.ItemResult.Value)
            .Where(r => r != null)
            .ToList()!;
    }
}