using System;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Duplicate Action Presses Between Hotbars")]
[TweakDescription("Shows the pulse effect when activating actions, even if they are duplicated between hotbars.")]
[TweakAuthor("BoredDan")]
[TweakVersion(2)]
[Changelog("1.9.1.0", "Fixed crashes and re-enabled tweak.")]
public unsafe class ActionPressMirroring : UiAdjustments.SubTweak {
    private delegate void PulseActionBarSlot(AddonActionBarBase* addonActionBarBase, uint slotIndex, ulong a3, int a4);

    [TweakHook, Signature("85 d2 78 ?? 48 89 5c 24 ?? 57 48 83 ec ?? 48 63 da 48 8b f9 48 8b 89 ?? ?? ?? ?? ba", DetourName = nameof(PulseActionBarSlotDetour))]
    private readonly HookWrapper<PulseActionBarSlot> pulseActionBarSlotHook = null!;

    private static readonly string[] AllActionBars = {
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
        "_ActionBarEx",
    };

    private void PulseActionBarSlotDetour(AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4)
    {
        HandlePulse(ab, slotIndex, a3, a4);
        pulseActionBarSlotHook.Original(ab, slotIndex, a3, a4);
    }

    private uint GetAdjustedId(RaptureHotbarModule.HotbarSlotType type, uint id) => type switch {
        RaptureHotbarModule.HotbarSlotType.Action => ActionManager.Instance()->GetAdjustedActionId(id),
        _ => id
    };

    private void HandlePulse(AddonActionBarBase* ab, uint slotIndex, ulong a3, int a4) {
        try {
            var slot = RaptureHotbarModule.Instance()->GetSlotById(ab->RaptureHotbarId, slotIndex);
            var id = GetAdjustedId(slot->CommandType, slot->CommandId);
            foreach (var barName in AllActionBars) {
                if (Common.GetUnitBase<AddonActionBarBase>(out var bar, barName)) {
                    for (var i = 0U; i < bar->SlotCount; i++) {
                        if (bar == ab && i == slotIndex) continue;
                        var barSlot = RaptureHotbarModule.Instance()->GetSlotById(bar->RaptureHotbarId, i);
                        if (barSlot->CommandType == slot->CommandType && GetAdjustedId(barSlot->CommandType, barSlot->CommandId) == id) {
                            pulseActionBarSlotHook.Original(bar, i, a3, a4);
                        }
                    }
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Error in HandlePulse");
        }

    }
}
