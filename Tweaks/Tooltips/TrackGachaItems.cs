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

[TweakName("Track Gacha Items")]
[TweakDescription("Adds the collectable checkmark to gacha items, such as Triple Triad card packs, when all potential items have been obtained.")]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.8.7.3", "Added 'Platinum Triad Card'")]
public unsafe class TrackGachaItems : TooltipTweaks.SubTweak {
    private static readonly Dictionary<uint, uint[]> Gachas = new() {
        // Bronze Triad Card 
        [10128] = [9782, 9809, 9797, 9796, 9779, 16762, 9783, 16760, 16759, 9776, 9798, 9775, 9795, 16765, 15621],
        // Silver Triad Card 
        [10129] = [9785, 14199, 9813, 9814, 9811, 9786, 9788, 9828, 9827, 9792, 9787, 9790, 9812, 9821],
        // Gold Triad Card
        [10130] = [9800, 9829, 9805, 14192, 9837, 9825, 9836, 9799, 9801, 9824, 9838, 9826, 9822, 9839, 9847],
        // Mythril Triad Card 
        [13380] = [9843, 14193, 13368, 9810, 9823, 9841, 13372, 9844, 13367],
        // Imperial Triad Card 
        [17702] = [17686, 16775, 17681, 17682, 16774, 13378],
        // Dream Triad Card    
        [28652] = [28661, 26767, 28657, 28653, 28655, 26772, 28658, 28660, 26765, 26768, 26766],
        // Platinum Triad Card 
        [10077] = [9830, 9842, 9840, 14208, 15872, 9828, 9851, 9831, 9834, 9826, 9822, 9848],

        // Materiel Container 3.0 
        [36635] = [
            9350, 12051, 6187, 15441, 6175, 7564, 6186, 6203, 6177, 17525, 15440, 14098, 6003, 12055, 6199, 6205,
            16570, 16568, 6189, 15447, 8193, 9347, 14103, 12054, 8194, 12061, 6191, 12069, 13279, 6179, 12058, 13283,
            12056, 9348, 7568, 6004, 8196, 8201, 7566, 10071, 6204, 6173, 14100, 9349, 8200, 8205, 16564, 8202, 12052,
            12057, 13275, 7559, 6192, 16572, 6208, 6195, 12062, 7567, 6188, 6174, 8199, 6185, 8195, 12053, 12049, 6005,
            6213, 6200, 6190, 16573, 17527, 14093, 13284, 13276, 14095, 6214, 15436, 15437, 14094, 6184, 14083, 6183, 6198,
            8192, 6209, 6178
        ],

        // Materiel Container 4.0 
        [36636] = [
            24902, 21921, 21063, 20529, 20530, 21920, 24002, 20524, 24635, 23027, 24001, 23023, 20533, 24219, 24630, 21052,
            20542, 24903, 20538, 21064, 20541, 21058, 20536, 23032, 23998, 20525, 21916, 20531, 21193, 23989, 24634, 21059,
            21922, 21919, 20528, 21911, 20547, 20539, 24000, 21918, 21055, 20544, 20546, 21915, 21060, 21917, 20537, 21057,
            23030, 21065, 20545, 23028, 24639, 23036, 24640
        ],
    };

    [LinkHandler(LinkHandlerId.TrackGachaItemsIdentifier)]
    private DalamudLinkPayload identifier;

    [TweakHook(typeof(UIState), nameof(UIState.IsItemActionUnlocked), nameof(IsItemActionUnlockedDetour))]
    private HookWrapper<UIState.Delegates.IsItemActionUnlocked>? isItemActionUnlockedHookWrapper;

    private bool IsGachaFullyObtained(uint[] gachaList, out int obtainedCount) {
        obtainedCount = 0;
        var allObtained = true;
        foreach (var i in gachaList) {
            var gachaResultItem = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(i);
            if (gachaResultItem == null || gachaResultItem.Value.ItemAction.RowId == 0) continue;

            var obtained = false;
            var action = gachaResultItem.Value.ItemAction;
            if (!action.IsValid || action.RowId == 0) continue;
            switch (action.Value.Action.RowId) {
                case 1322:
                    // Mount
                    obtained = UIState.Instance()->PlayerState.IsMountUnlocked(action.Value.Data[0]);
                    break;
                case 853:
                    // Minion
                    obtained = UIState.Instance()->IsCompanionUnlocked(action.Value.Data[0]);
                    break;
                case 3357:
                    // Triad Card
                    obtained = UIState.Instance()->IsTripleTriadCardUnlocked((ushort)gachaResultItem.Value.AdditionalData.RowId);
                    break;
                default:
                    Plugin.Error(this, new Exception($"Unhandled Item Action Type: {action.Value.Action.RowId}"), true);
                    break;
            }

            if (!obtained) {
                allObtained = false;
            } else {
                obtainedCount++;
            }
        }

        return allObtained;
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (!Gachas.TryGetValue(Item.ItemId, out var gachaList)) return;
        var fullyObtained = IsGachaFullyObtained(gachaList, out var obtainedCount);
        var description = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription);

        if (description.Payloads.Any(payload => payload is DalamudLinkPayload dlp && dlp.CommandId == identifier.CommandId)) return; // Don't append when it already exists.

        description.Payloads.Add(identifier);
        description.Payloads.Add(RawPayload.LinkTerminator);

        if (fullyObtained) {
            description.Payloads.Add(new NewLinePayload());
            description.Payloads.Add(new UIForegroundPayload(500));
            description.Payloads.Add(new TextPayload("All Options Obtained"));
            description.Payloads.Add(new UIForegroundPayload(0));
        } else {
            description.Payloads.Add(new NewLinePayload());
            description.Payloads.Add(new UIForegroundPayload(500));
            description.Payloads.Add(new TextPayload($"Obtained: {obtainedCount}/{gachaList.Length}"));
            description.Payloads.Add(new UIForegroundPayload(0));
        }

        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, description);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private long IsItemActionUnlockedDetour(UIState* uiState, void* item) {
        var loadedItem = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(LoadedItem);
        if (loadedItem != null && Gachas.TryGetValue(loadedItem.Value.RowId, out var gachaList)) {
            return (byte)(IsGachaFullyObtained(gachaList, out _) ? 1 : 2);
        }

        return isItemActionUnlockedHookWrapper!.Original(uiState, item);
    }
}
