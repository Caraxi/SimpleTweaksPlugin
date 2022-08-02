using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public unsafe class TrackFadedRolls : TooltipTweaks.SubTweak {
    // Thank you to ascclemens for the inspiration to do this through GoodMemory, as well as the base model for how to
    // get the crafted orchestrion rolls from the faded ones.
    
    public override string Name => "Track Faded Orchestrion Rolls";
    protected override string Author => "KazWolfe";
    public override string Description => "Adds the collected checkmark to Faded Orchestrion Rolls.";

    private const string IsItemUnlockedSignature = "E8 ?? ?? ?? ?? 83 F8 01 75 03";
    private const string IsOrchestrionUnlockedSignature = "E8 ?? ?? ?? ?? 88 44 3B 08";
    private const string UnlockBitmaskSignature = "48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC 40 53";
    
    private delegate byte IsItemActionUnlocked(UIState* uiState, IntPtr item);
    private HookWrapper<IsItemActionUnlocked>? _isItemActionUnlockedHookWrapper;

    private delegate byte IsOrchestrionUnlockedDelegate(IntPtr mask, uint id);
    private IsOrchestrionUnlockedDelegate? _isOrchestrionUnlocked;

    private IntPtr _superUnlockBitmask;
    
    public override void Enable() {
        if (this._isOrchestrionUnlocked == null &&
            Service.SigScanner.TryScanText(IsOrchestrionUnlockedSignature, out var ptr)) {
            this._isOrchestrionUnlocked = Marshal.GetDelegateForFunctionPointer<IsOrchestrionUnlockedDelegate>(ptr);
        }

        this._superUnlockBitmask = Service.SigScanner.GetStaticAddressFromSig(UnlockBitmaskSignature);

        // We *need* this signature. If it doesn't exist, don't load the tweak.
        if (this._isOrchestrionUnlocked == null)
            throw new InvalidOperationException("Cannot find signature for IsOrchestrionUnlocked!");

        this._isItemActionUnlockedHookWrapper ??=
            Common.Hook<IsItemActionUnlocked>(IsItemUnlockedSignature, this.IsItemActionUnlockedDetour);
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
            .TrueForAll(o => this._isOrchestrionUnlocked!(this._superUnlockBitmask, o.AdditionalData) == 1);

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