using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

[TweakName("Track Faded Orchestrion Rolls")]
[TweakDescription("Adds the collectable checkmark to Faded Orchestrion Rolls.")]
[TweakAuthor("KazWolfe")]
[Changelog("1.8.7.0", "Fixed tweak not functioning at all.")]
[Changelog("1.9.3.0", "Added tracking for faded rolls with multiple crafts.", Author = "KazWolfe")]
public unsafe class TrackFadedRolls : TooltipTweaks.SubTweak {
    // Thank you to ascclemens for the inspiration to do this through GoodMemory, as well as the base model for how to
    // get the crafted orchestrion rolls from the faded ones.
    
    [TweakHook(typeof(UIState), nameof(UIState.IsItemActionUnlocked), nameof(IsItemActionUnlockedDetour))]
    private HookWrapper<UIState.Delegates.IsItemActionUnlocked> isItemActionUnlockedHookWrapper;

    private DalamudLinkPayload identifier;

    protected override void Enable() {
        identifier = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.TrackFadedRollsIdentifier, (_, _) => { });
    }

    protected override void Disable() {
        PluginInterface.RemoveChatLinkHandler((uint)LinkHandlerId.TrackFadedRollsIdentifier);
    }
    
   private long IsItemActionUnlockedDetour(UIState* uiState, void* item) {
        if (!IsHoveredItemOrchestrion(out var luminaItem)) {
           return isItemActionUnlockedHookWrapper!.Original(uiState, item);
        }

        var areAllUnlocked = GetRollsCraftedWithItem(luminaItem)
            .TrueForAll(o => UIState.Instance()->PlayerState.IsOrchestrionRollUnlocked(o.AdditionalData));

        return areAllUnlocked ? 1 : 2;
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
            .Where(r => r.ItemResult.IsValid)
            .Select(r => r.ItemResult.Value)
            .ToList()!;
    }

    private static bool IsHoveredItemOrchestrion(out Item item) => Service.Data.GetExcelSheet<Item>().TryGetRow(Item.ItemId, out item) && item is { FilterGroup: 12, ItemUICategory.RowId: 94, LevelItem.RowId: 1 };
}
