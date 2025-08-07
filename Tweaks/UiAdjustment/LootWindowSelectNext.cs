#nullable enable
using System.Linq;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Loot Window Select Next Item")]
[TweakDescription("Upon pressing 'Need', 'Greed', or 'Pass' automatically select the next loot item.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.9.2")]
public unsafe class LootWindowSelectNext : UiAdjustments.SubTweak {
    [AddonPostSetup("NeedGreed")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        // Find first item that hasn't been rolled on, and select it.
        var addonNeedGreed = (AddonNeedGreed*) atkUnitBase;
        foreach (var index in Enumerable.Range(0, addonNeedGreed->NumItems)) {
            if (addonNeedGreed->Items[index] is { Roll: 0, ItemId: not 0 }) {
                SelectItem(addonNeedGreed, index);
                break;
            }
        }
    }

    [AddonPostReceiveEvent("NeedGreed")]
    private void OnNeedGreedReceiveEvent(AddonReceiveEventArgs args) {
        var type = (AtkEventType) args.AtkEventType;
        var buttonType = (ButtonType) args.EventParam;
        var addon = (AddonNeedGreed*) args.Addon.Address;
        
        if (type is not AtkEventType.ButtonClick) return;
        
        switch (buttonType) {
            // Fall through unconditionally
            case ButtonType.Need:
            case ButtonType.Greed:
            
            // Don't select next item if we are passing on an item that we already rolled on
            case ButtonType.Pass when addon->Items[addon->SelectedItemIndex] is { Roll: 0, ItemId: not 0 }: 
                var currentItemCount = addon->NumItems;
                var nextIndex = addon->SelectedItemIndex + 1;

                if (nextIndex < currentItemCount) {
                    SelectItem(addon, nextIndex);
                }
                break;
        }
    }

    private static void SelectItem(AddonNeedGreed* addon, int index) {
        var eventData = new AtkEventData();
        eventData.ListItemData.SelectedIndex = index;
        addon->ReceiveEvent(AtkEventType.ListItemClick, 0, null, &eventData);
    }
    
    // There are other button types such as "Greed Only" and "Loot Recipient"
    private enum ButtonType : uint {
        Need = 0,
        Greed = 1,
        Pass = 2,
    }
}