using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Linq;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;
[TweakName("Track Outfits")]
[TweakDescription("Shows whether or not you've made an outfit out of the hovered item.")]
[TweakAuthor("croizat")]
[TweakReleaseVersion("1.10.8.0")]
public unsafe class TrackOutfits : TooltipTweaks.SubTweak
{
    [TweakHook(typeof(UIState), nameof(UIState.IsItemActionUnlocked), nameof(IsItemActionUnlockedDetour))]
    private HookWrapper<UIState.Delegates.IsItemActionUnlocked> isItemActionUnlockedHookWrapper;

    [LinkHandler(LinkHandlerId.TrackOutfitsIdentifier)]
    private DalamudLinkPayload identifier;
    private uint[] OwnedOutfits
    {
        get
        {
            var agent = ItemFinderModule.Instance();
            if (agent == null) return [];
            return agent->GlamourDresserItemIds.ToArray().Where(x => x != 0 && Service.Data.GetExcelSheet<MirageStoreSetItem>().HasRow(x)).Select(x => ItemUtil.GetBaseId(x).ItemId).ToArray();
        }
    }

    private long IsItemActionUnlockedDetour(UIState* uiState, void* item)
    {
        if (GetOutfits(ItemUtil.GetBaseId(Item.ItemId).ItemId) is { Length: > 0 } outfits)
        {
            foreach (var o in outfits)
                if (!OwnedOutfits.Contains(o))
                    return 2;
            return 1;
        }
        return isItemActionUnlockedHookWrapper!.Original(uiState, item);
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        if (GetOutfits(ItemUtil.GetBaseId(Item.ItemId).ItemId) is { Length: > 0 } outfits)
        {
            var description = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription);

            if (description.Payloads.Any(payload => payload is DalamudLinkPayload dlp && dlp.CommandId == identifier.CommandId))
                return; // Don't append when it already exists.

            description.Payloads.Add(identifier);
            description.Payloads.Add(RawPayload.LinkTerminator);

            description.Payloads.Add(new NewLinePayload());
            description.Payloads.Add(new TextPayload("Outfits"));

            foreach (var outfit in outfits)
            {
                var isOutfitOwned = OwnedOutfits.Contains(outfit);

                description.Payloads.Add(new NewLinePayload());
                description.Payloads.Add(new UIForegroundPayload((ushort)(isOutfitOwned ? 45 : 14)));
                description.Payloads.Add(new TextPayload($"    {Service.Data.GetExcelSheet<Item>().GetRow(outfit).Name} (Acquired: {(isOutfitOwned ? "Yes" : "No")})"));
                description.Payloads.Add(new UIForegroundPayload(0));
            }

            try
            {
                SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription, description);
            }
            catch (Exception ex)
            {
                SimpleLog.Error(ex);
            }
        }
    }

    private static uint[] GetOutfits(uint itemId)
    {
        return Service.Data.GetExcelSheet<MirageStoreSetItemLookup>()
            .Where(row => row.RowId == itemId)
            .SelectMany(row => row.Item.Where(x => x.Value.RowId != 0))
            .Select(x => x.Value.RowId)
            .ToArray();
    }
}
