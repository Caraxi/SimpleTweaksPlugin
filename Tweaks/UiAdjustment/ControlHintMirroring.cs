using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Duplicate Keybind Hints Between Hotbars")]
[TweakDescription("Will display the keybind hint for any hotbar slot onto unbound slots with the same action.")]
[TweakAuthor("BoredDan")]
[TweakVersion(2)]
[Changelog("1.9.1.0", "Rewritten & re-enabled")]
public unsafe class ControlHintMirroring : UiAdjustments.SubTweak {
    private readonly Dictionary<(HotbarSlotType Type, uint Id), string> hints = new();
    private readonly Dictionary<(byte BarID, byte SlotIndex), bool> barSlotHasNoHint = new();
    private readonly Dictionary<(byte BarID, byte SlotIndex), bool> barSlotIsSet = new();
    private readonly string[] actionBars = { "_ActionBar09", "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08" };
    // const arrays please?
    [AddonPreRequestedUpdate("_ActionBar09", "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08")]
    private void ActionBarUpdateRequested(AddonActionBarX* addon) {
        var barId = addon->AddonActionBarBase.RaptureHotbarId;
        if (barId == 9) hints.Clear(); // Relies on the game updating action bars in reverse order
        for (var slotIndex = (byte)(addon->AddonActionBarBase.SlotCount - 1); slotIndex < addon->AddonActionBarBase.SlotCount; slotIndex--) {
            if (barSlotIsSet.TryGetValue((barId, slotIndex), out var isSet) && isSet) {
                addon->AddonActionBarBase.Slot[slotIndex].ControlHintTextNode->SetText(string.Empty);
                barSlotIsSet[(barId, slotIndex)] = false;
            }

            var slot = RaptureHotbarModule.Instance()->GetSlotById(barId, slotIndex);
            if (slot->CommandType == HotbarSlotType.Empty) continue;

            var str = Common.ReadString(slot->KeybindHint, 0x10);
            if (string.IsNullOrWhiteSpace(str)) {
                barSlotHasNoHint[(barId, slotIndex)] = true;
                continue;
            }

            var commandId = slot->CommandType == HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
            hints[(slot->CommandType, commandId)] = str;
            barSlotHasNoHint[(barId, slotIndex)] = false;
        }
        
        if (barId == 0 && !Unloading) Service.Framework.RunOnTick(FrameworkUpdate);
    }
    
    private void FrameworkUpdate() {
        using var _ = PerformanceMonitor.Run();
        foreach (var bar in actionBars) {
            if (!Common.GetUnitBase<AddonActionBarX>(out var addon, bar)) continue;
            var barId = addon->AddonActionBarBase.RaptureHotbarId;
            for (byte slotIndex = 0; slotIndex < addon->AddonActionBarBase.SlotCount; slotIndex++) {
                var slot = RaptureHotbarModule.Instance()->GetSlotById(barId, slotIndex);
                if (slot->CommandType == HotbarSlotType.Empty) continue;
                if (barSlotHasNoHint.TryGetValue((barId, slotIndex), out var noHint) && noHint) {
                    var commandId = slot->CommandType == HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
                    if (hints.TryGetValue((slot->CommandType, commandId), out var hint)) {
                        addon->AddonActionBarBase.Slot[slotIndex].ControlHintTextNode->SetText(hint);
                        barSlotIsSet[(barId, slotIndex)] = true;
                    } else if (barSlotIsSet.TryGetValue((barId, slotIndex), out var isSet) && isSet) {
                        addon->AddonActionBarBase.Slot[slotIndex].ControlHintTextNode->SetText(string.Empty);
                        barSlotIsSet[(barId, slotIndex)] = false;
                    }
                }
            }
        }
    }

    private void UpdateAll() {
        foreach(var addonName in actionBars) {
            if (Common.GetUnitBase<AddonActionBarX>(out var addon, addonName)) ActionBarUpdateRequested(addon);
        }
    }

    protected override void Enable() => UpdateAll();
    protected override void Disable() => UpdateAll();
}