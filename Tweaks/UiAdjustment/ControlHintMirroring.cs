using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
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
[Changelog("1.9.1.1", "Fixed flickering while cooldown is active.")]
public unsafe class ControlHintMirroring : UiAdjustments.SubTweak {
    [StructLayout(LayoutKind.Explicit, Size = 0xC8)]
    public unsafe struct ActionBarSlotTemp {
        [FieldOffset(0x00)] public AtkComponentDragDrop* ComponentDragDrop;
        [FieldOffset(0x08)] public AtkImageNode* ChargeIcon;
        [FieldOffset(0x10)] public AtkResNode* RecastOverlayContainer;
        [FieldOffset(0x18)] public AtkResNode* IconFrame;
        [FieldOffset(0x20)] public CStringPointer PopUpHelpTextPtr;
        [FieldOffset(0x30)] public int HotbarId;
        [FieldOffset(0x34)] public int ActionId;
        [FieldOffset(0xB8)] public AtkComponentNode* Icon;
        [FieldOffset(0xC0)] public AtkTextNode* ControlHintTextNode;
    }
    
    private readonly Dictionary<(RaptureHotbarModule.HotbarSlotType Type, uint Id), string> hints = new();
    private readonly Dictionary<(byte BarID, byte SlotIndex), bool> barSlotHasNoHint = new();
    private readonly Dictionary<(byte BarID, byte SlotIndex), bool> barSlotIsSet = new();
    private readonly string[] actionBars = ["_ActionBar09", "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08"];

    // const arrays please?
    [AddonPreRequestedUpdate("_ActionBar09", "_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08")]
    private void ActionBarUpdateRequested(AddonActionBarX* addon) {
        var barId = addon->AddonActionBarBase.RaptureHotbarId;
        if (barId == 9) hints.Clear(); // Relies on the game updating action bars in reverse order
        for (var slotIndex = (byte)(addon->AddonActionBarBase.SlotCount - 1); slotIndex < addon->AddonActionBarBase.SlotCount; slotIndex--) {
            if (barSlotIsSet.TryGetValue((barId, slotIndex), out var isSet) && isSet) {
                var addonSlot = (ActionBarSlotTemp*) addon->ActionBarSlotVector.GetPointer(slotIndex);
                addonSlot->ControlHintTextNode->SetText(string.Empty);
                barSlotIsSet[(barId, slotIndex)] = false;
            }

            var slot = RaptureHotbarModule.Instance()->GetSlotById(barId, slotIndex);
            if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty) continue;

            var str = slot->KeybindHintString;
            if (string.IsNullOrWhiteSpace(str)) {
                barSlotHasNoHint[(barId, slotIndex)] = true;
                continue;
            }

            var commandId = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
            hints[(slot->CommandType, commandId)] = str;
            barSlotHasNoHint[(barId, slotIndex)] = false;
        }

        if (barId == 0 && !Unloading) ApplyHints();
    }

    private void ApplyHints() {
        using var _ = PerformanceMonitor.Run();
        foreach (var bar in actionBars) {
            if (!Common.GetUnitBase<AddonActionBarX>(out var addon, bar)) continue;
            var barId = addon->AddonActionBarBase.RaptureHotbarId;
            for (byte slotIndex = 0; slotIndex < addon->AddonActionBarBase.SlotCount; slotIndex++) {
                var slot = RaptureHotbarModule.Instance()->GetSlotById(barId, slotIndex);
                if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty) continue;
                if (barSlotHasNoHint.TryGetValue((barId, slotIndex), out var noHint) && noHint) {
                    var commandId = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
                    var addonSlot = (ActionBarSlotTemp*) addon->ActionBarSlotVector.GetPointer(slotIndex);
                    if (hints.TryGetValue((slot->CommandType, commandId), out var hint)) {
                        addonSlot->ControlHintTextNode->SetText(hint);
                        barSlotIsSet[(barId, slotIndex)] = true;
                    } else if (barSlotIsSet.TryGetValue((barId, slotIndex), out var isSet) && isSet) {
                        addonSlot->ControlHintTextNode->SetText(string.Empty);
                        barSlotIsSet[(barId, slotIndex)] = false;
                    }
                }
            }
        }
    }

    private void UpdateAll() {
        foreach (var addonName in actionBars) {
            if (Common.GetUnitBase<AddonActionBarX>(out var addon, addonName)) ActionBarUpdateRequested(addon);
        }
    }

    protected override void Enable() => UpdateAll();
    protected override void Disable() => UpdateAll();
}
