using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class TrackGachaItems : TooltipTweaks.SubTweak {
    public override string Name => "Track Gacha Items";
    public override string Description => "Adds the collectable checkmark to gacha items, such as Triple Triad card packs, when all potential items have been obtained.";

    private Dictionary<uint, uint[]> Gachas = new() {
        /* Bronze Triad Card   */ [10128] = new uint[]{ 9782, 9809, 9797, 9796, 9779, 16762, 9783, 16760, 16759, 9776, 9798, 9775, 9795, 16765, 15621 },
        /* Silver Triad Card   */ [10129] = new uint[]{ 9785, 14199, 9813, 9814, 9811, 9786, 9788, 9828, 9827, 9792, 9787, 9790, 9812, 9821 },
        /* Gold Triad Card     */ [10130] = new uint[]{ 9800, 9829, 9805, 14192, 9837, 9825, 9836, 9799, 9801, 9824, 9838, 9826, 9822, 9839, 9847 },
        /* Mythril Triad Card  */ [13380] = new uint[]{ 9843, 14193, 13368, 9810, 9823, 9841, 13372, 9844, 13367 },
        /* Imperial Triad Card */ [17702] = new uint[]{ 17686, 16775, 17681, 17682, 16774, 13378 },
        /* Dream Triad Card    */ [28652] = new uint[]{ 28661, 26767, 28657, 28653, 28655, 26772, 28658, 28660, 26765, 26768, 26766 },
    };

    private DalamudLinkPayload? identifier;
    

    private delegate byte IsItemActionUnlocked(UIState* uiState, nuint item);
    private HookWrapper<IsItemActionUnlocked>? _isItemActionUnlockedHookWrapper;

    public override void Setup() {
        AddChangelogNewTweak(Changelog.UnreleasedVersion);
        base.Setup();
    }

    public override void Enable() {
        this._isItemActionUnlockedHookWrapper ??=
            Common.Hook<IsItemActionUnlocked>(UIState.Addresses.IsItemActionUnlocked.Value, this.IsItemActionUnlockedDetour);
        this._isItemActionUnlockedHookWrapper?.Enable();
        
        identifier = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.TrackGachaItemsIdentifier, (_, _) => { });

        base.Enable();
    }

    private bool IsGachaFullyObtained(uint[] gachaList, out int obtainedCount) {
        obtainedCount = 0;
        var allObtained = true;
        foreach (var i in gachaList) {
            var gachaResultItem = Service.Data.Excel.GetSheet<Item>()?.GetRow(i);
            if (gachaResultItem == null || gachaResultItem.ItemAction.Row == 0) continue;

            var obtained = false;
            switch (gachaResultItem.ItemAction.Value.Type) {
                case 3357:
                    // Triad Card
                    obtained = UIState.Instance()->IsTripleTriadCardUnlocked((ushort)gachaResultItem.AdditionalData);
                    break;
                default:
                    Plugin.Error(this, new Exception($"Unhandled Item Action Type: {gachaResultItem.ItemAction.Value.Type}"), true);
                    Disable();
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
        if (Gachas.TryGetValue(Item.ItemID, out var gachaList)) {

            var fullyObtained = IsGachaFullyObtained(gachaList, out var obtainedCount);
            var description = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemDescription);

            if (description.Payloads.Any(payload => payload is DalamudLinkPayload { CommandId: (uint)LinkHandlerId.TrackGachaItemsIdentifier })) return; // Don't append when it already exists.
            
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
            SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, description);
        }

    }

    private byte IsItemActionUnlockedDetour(UIState* uiState, nuint item) {
        var loadedItem = Service.Data.Excel.GetSheet<Item>()?.GetRow(LoadedItem);
        if (loadedItem != null && *(ushort*)(item + 0x88) == loadedItem.Icon && Gachas.TryGetValue(loadedItem.RowId, out var gachaList)) {
            return (byte)(IsGachaFullyObtained(gachaList, out _) ? 1 : 2);
        }
        
        return _isItemActionUnlockedHookWrapper.Original(uiState, item);
    }

    public override void Disable() {
        _isItemActionUnlockedHookWrapper?.Disable();
        PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.TrackGachaItemsIdentifier);
        base.Disable();
    }

    public override void Dispose() {
        _isItemActionUnlockedHookWrapper?.Dispose();
        base.Dispose();
    }
}

