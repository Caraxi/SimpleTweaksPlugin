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
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Text;
using Lumina.Text.Payloads;
using TextPayload = Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;
[TweakName("Track Outfits")]
[TweakDescription("Shows whether or not you've made an outfit out of the hovered item.")]
[TweakAuthor("croizat")]
[TweakReleaseVersion("1.10.8.0")]
[TweakAutoConfig]
[Changelog("1.14.0.0", "Fixed HQ outfits not working.")]
[Changelog("1.15.0.5", "Added option to ignore quality when checking if an item exists as part of an outfit.")]
[Changelog("1.15.0.7", "Fixed some key items incorrectly indicating they belong to an outfit.")]
public unsafe class TrackOutfits : TooltipTweaks.SubTweak
{
    [TweakHook(typeof(UIState), nameof(UIState.IsItemActionUnlocked), nameof(IsItemActionUnlockedDetour))]
    private HookWrapper<UIState.Delegates.IsItemActionUnlocked> isItemActionUnlockedHookWrapper;

    [LinkHandler(LinkHandlerId.TrackOutfitsIdentifier)]
    private DalamudLinkPayload identifier;
    
    private record OwnedOutfit(uint SetId, ItemKind ItemKind, HashSet<uint> OwnedItems, HashSet<uint> MissingItems);
    private DateTime ownedOutfitCacheLastUpdate = DateTime.MinValue;
    
    public class Configs : TweakConfig {
        [TweakConfigOption("Mark HQ items as obtained if their NQ variant is obtained, or vice versa")]
        public bool IgnoreQuality = true;
    }
    
    [TweakConfig] public Configs TweakConfig { get; private set; } = new();
    
    private List<OwnedOutfit> OwnedOutfits {
        get {
            var cacheTime = AgentMiragePrismPrismBox.Instance()->IsAgentActive() ? 1 : 30;
            if (DateTime.Now - ownedOutfitCacheLastUpdate < TimeSpan.FromSeconds(cacheTime)) {
                return field;
            }
            ownedOutfitCacheLastUpdate = DateTime.Now;
            var agent = ItemFinderModule.Instance();
            var l = new List<OwnedOutfit>();
            for (ushort i = 0; i < agent->GlamourDresserItemIds.Length && i < agent->GlamourDresserItemSetUnlockBits.Length; i++) {
                var item = ItemUtil.GetBaseId(agent->GlamourDresserItemIds[i]);
                if (item.ItemId == 0) continue;
                if (!Service.Data.GetExcelSheet<MirageStoreSetItem>().TryGetRow(item.ItemId , out var row)) continue;
                var ownedItems = new HashSet<uint>();
                var missingItems = new HashSet<uint>();
                var bits = ItemFinderModule.Instance()->GlamourDresserItemSetUnlockBits[i];
                for (int j = 0; j < 11; j++) {
                    RowRef<Item>? slot = j switch {
                        0 => row.MainHand,
                        1 => row.OffHand,
                        2 => row.Head,
                        3 => row.Body,
                        4 => row.Hands,
                        5 => row.Legs,
                        6 => row.Feet,
                        7 => row.Earrings,
                        8 => row.Necklace,
                        9 => row.Bracelets,
                        10 => row.Ring,
                        _ => null
                    };
                    
                    if (slot == null || slot.Value.RowId == 0) continue;
                    
                    if (MirageManager.Instance()->PrismBoxLoaded) {
                        if (MirageManager.Instance()->IsSetSlotUnlocked(i, j)) {
                            ownedItems.Add(slot.Value.RowId);
                        } else {
                            missingItems.Add(slot.Value.RowId);
                        }
                    } else {
                        if (((bits >> j) & 1) == 0) {
                            ownedItems.Add(slot.Value.RowId);
                        } else {
                            missingItems.Add(slot.Value.RowId);
                        }
                    }
                }

                l.Add(new OwnedOutfit(item.ItemId, item.Kind, ownedItems, missingItems));
            }
            return field = l;
        }
    } = new();

    private long IsItemActionUnlockedDetour(UIState* uiState, void* item) {
        var baseId = ItemUtil.GetBaseId((Item.ItemId % 1000000) + (Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) ? 1000000U : 0U));
        if (GetOutfits(baseId.ItemId) is { Length: > 0 } outfits)
        {
            foreach (var o in outfits)
                if (!OwnedOutfits.Any(oo => oo.SetId == o && (TweakConfig.IgnoreQuality || oo.ItemKind == baseId.Kind) && oo.OwnedItems.Contains(baseId.ItemId)))
                    return 2;
            return 1;
        }
        return isItemActionUnlockedHookWrapper!.Original(uiState, item);
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        if (Item.ItemId >= 2000000) return;
        var baseId = ItemUtil.GetBaseId((Item.ItemId % 1000000) + (Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) ? 1000000U : 0U));
        if (GetOutfits(baseId.ItemId) is { Length: > 0 } outfits) {

            if (!TryGetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription, out var originalDescription)) return;
            if (originalDescription.ContainsDalamudLinkPayload(identifier)) return;

            var descriptionBuilder = new SeStringBuilder()
                .Append(originalDescription)
                .AppendDalamudLinkPayload(identifier).PopLink()
                .AppendNewLine()
                .Append("Outfits");

            foreach (var outfit in outfits) {
                var ownedOutfit = OwnedOutfits.Where(oo => oo.SetId == outfit && (TweakConfig.IgnoreQuality || oo.ItemKind == baseId.Kind));
                var isOutfitOwned = ownedOutfit.Any(oo => oo.OwnedItems.Contains(baseId.ItemId));
                var isOwnedInAnotherQuality = !TweakConfig.IgnoreQuality && OwnedOutfits.Any(oo => oo.SetId == outfit && oo.OwnedItems.Contains(baseId.ItemId));
                descriptionBuilder.AppendNewLine()
                    .PushColorType(isOutfitOwned ? 45U : isOwnedInAnotherQuality ? 26U : 14U)
                    .Append($"    {Service.Data.GetExcelSheet<Item>().GetRow(outfit).Name} (Acquired: {(isOutfitOwned ? "Yes" : isOwnedInAnotherQuality ? $"As {(baseId.Kind == ItemKind.Hq ? "NQ" : "HQ")}" : "No")})")
                    .PopColorType();
            }

            try {
                SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription, descriptionBuilder.GetViewAsSpan());
            } catch (Exception ex) {
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
