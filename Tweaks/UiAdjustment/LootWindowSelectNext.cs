#nullable enable
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Loot Window Select Next Item")]
[TweakDescription("Upon pressing 'Need', 'Greed', or 'Pass' automatically select the next loot item.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.9.2")]
public unsafe class LootWindowSelectNext : UiAdjustments.SubTweak {

    [TweakHook(AutoEnable = false)]
    private HookWrapper<AddonNeedGreed.Delegates.ReceiveEvent>? needGreedReceiveEventHook;

    [AddonPostSetup("NeedGreed")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        needGreedReceiveEventHook ??= Common.Hook<AddonNeedGreed.Delegates.ReceiveEvent>(atkUnitBase->VirtualTable->ReceiveEvent, OnNeedGreedReceiveEvent);
        needGreedReceiveEventHook?.Enable();

        // Find first item that hasn't been rolled on, and select it.
        var addonNeedGreed = (AddonNeedGreed*) atkUnitBase;
        foreach (var index in Enumerable.Range(0, addonNeedGreed->NumItems)) {
            if (addonNeedGreed->Items[index] is { Roll: 0, ItemId: not 0 }) {
                SelectItem(addonNeedGreed, index);
                break;
            }
        }
    }

    // There are other button types such as "Greed Only" and "Loot Recipient"
    private enum ButtonType : uint {
        Need = 0,
        Greed = 1,
        Pass = 2,
    }

    private void OnNeedGreedReceiveEvent(AddonNeedGreed* addon, AtkEventType type, int eventParam, AtkEvent* eventInfo, AtkEventData* atkEventData) {
        needGreedReceiveEventHook!.Original(addon, type, eventParam, eventInfo, atkEventData);
        
        try {
            if (type is not AtkEventType.ButtonClick) return;

            var buttonType = (ButtonType) eventParam;

            switch (buttonType) {
                case ButtonType.Need:
                case ButtonType.Greed:
                case ButtonType.Pass when IsSelectedItemUnrolled(addon): // Don't select next item if we are passing on an item that we already rolled on
                    var currentItemCount = addon->NumItems;
                    var nextIndex = addon->SelectedItemIndex + 1;
                    
                    if (nextIndex < currentItemCount) SelectItem(addon, nextIndex);
                    break;
            }
        } catch (Exception exception) {
            SimpleLog.Error(exception, "Something went wrong in 'Loot Window Select Next Item', let MidoriKami know.");
        }
    }

    private void SelectItem(AddonNeedGreed* addon, int index) {
        var values = new AtkEventData();
        values.ListItemData.SelectedIndex = index;
        OnNeedGreedReceiveEvent(addon, AtkEventType.ListItemToggle, 0, null, &values); 
    }

    private LootItemInfo GetSelectedItem(AddonNeedGreed* addon) 
        => addon->Items[addon->SelectedItemIndex];

    private bool IsSelectedItemUnrolled(AddonNeedGreed* addon)
        => GetSelectedItem(addon) is { Roll: 0, ItemId: not 0 };
}