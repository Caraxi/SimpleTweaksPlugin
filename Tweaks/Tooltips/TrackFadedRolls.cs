using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JetBrains.Annotations;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public unsafe class TrackFadedRolls : TooltipTweaks.SubTweak {
    // Thank you to ascclemens for the inspiration to do this through GoodMemory, as well as the base model for how to
    // get the crafted orchestrion rolls from the faded ones.
    
    public override string Name => "Track Faded Orchestrion Rolls";
    protected override string Author => "KazWolfe";
    public override string Description => "Adds the collectable checkmark to Faded Orchestrion Rolls.";
    
    private delegate byte IsItemActionUnlocked(UIState* uiState, IntPtr item);
    private HookWrapper<IsItemActionUnlocked>? _isItemActionUnlockedHookWrapper;

    [CanBeNull] private DalamudLinkPayload identifier;

    public override void Setup() {
        base.Setup();
        AddChangelog("1.8.7.0", "Fixed tweak not functioning at all.");
        AddChangelog(UnreleasedVersion, "Added tracking for faded rolls with multiple crafts.").Author("KazWolfe");
    }

    protected override void Enable() {
        this._isItemActionUnlockedHookWrapper ??=
            Common.Hook<IsItemActionUnlocked>(UIState.Addresses.IsItemActionUnlocked.Value, this.IsItemActionUnlockedDetour);
        this._isItemActionUnlockedHookWrapper?.Enable();
        
        this.identifier = this.PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.TrackFadedRollsIdentifier, (_, _) => { });

        base.Enable();
    }

    protected override void Disable() {
        this._isItemActionUnlockedHookWrapper?.Disable();
        this.PluginInterface.RemoveChatLinkHandler((uint)LinkHandlerId.TrackFadedRollsIdentifier);
        
        base.Disable();
    }

    public override void Dispose() {
        this._isItemActionUnlockedHookWrapper?.Dispose();

        base.Dispose();
    }
    
   private byte IsItemActionUnlockedDetour(UIState* uiState, IntPtr item) {
        if (!IsHoveredItemOrchestrion(out var luminaItem)) {
           return this._isItemActionUnlockedHookWrapper!.Original(uiState, item);
        }
        
        if (luminaItem == null) return 4;

        var areAllUnlocked = GetRollsCraftedWithItem(luminaItem)
            .TrueForAll(o => UIState.Instance()->PlayerState.IsOrchestrionRollUnlocked(o.AdditionalData));

        return areAllUnlocked ? (byte)1 : (byte)2;
    }
    
    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (!IsHoveredItemOrchestrion(out var luminaItem)) return;
        if (luminaItem == null) return;

        var craftResults = GetRollsCraftedWithItem(luminaItem);
        if (craftResults.Count > 1) {
            var description = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription);
            
            if (description.Payloads.Any(payload => payload is DalamudLinkPayload { CommandId: (uint)LinkHandlerId.TrackFadedRollsIdentifier })) return; // Don't append when it already exists.

            description.Payloads.Add(identifier);
            description.Payloads.Add(RawPayload.LinkTerminator);
            
            description.Payloads.Add(new NewLinePayload());
            description.Payloads.Add(new TextPayload("Craftable Orchestrion Rolls"));
            
            foreach (var craftedRoll in craftResults) {
                var isRollUnlocked = UIState.Instance()->PlayerState.IsOrchestrionRollUnlocked(craftedRoll.AdditionalData);
                
                description.Payloads.Add(new NewLinePayload());
                description.Payloads.Add(new UIForegroundPayload((ushort)(isRollUnlocked ? 45 : 14)));
                description.Payloads.Add(new TextPayload($"    {craftedRoll.Name} (Acquired: {(isRollUnlocked ? "Yes" : "No")})"));
                description.Payloads.Add(new UIForegroundPayload(0));
            }
            
            try {
                SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, description);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
    }
    
    private static List<Item> GetRollsCraftedWithItem(Item item) {
        return Service.Data.Excel.GetSheet<Recipe>()!
            .Where(r => r.UnkData5.Any(i => i.ItemIngredient == item.RowId))
            .Select(r => r.ItemResult.Value)
            .Where(r => r != null)
            .ToList()!;
    }

    private static bool IsHoveredItemOrchestrion(out Item? item) {
        item = Service.Data.GetExcelSheet<Item>()!.GetRow(Item.ItemID);
        
        return item is { FilterGroup: 12, ItemUICategory.Row: 94, LevelItem.Row: 1 };
    }
}
