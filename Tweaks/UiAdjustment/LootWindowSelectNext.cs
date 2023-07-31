#nullable enable
using System;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Loot Window Select Next Item")]
[TweakDescription("Upon pressing 'Need', 'Greed', or 'Pass' automatically select the next loot item.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class LootWindowSelectNext : UiAdjustments.SubTweak
{
    private delegate void NeedGreedReceiveEventDelegate(AddonNeedGreed* addon, AtkEventType type, uint buttonType, AtkEvent* eventInfo, nint data);

    private Hook<NeedGreedReceiveEventDelegate>? needGreedReceiveEventHook;

    [AddonSetup("NeedGreed")]
    private void AddonSetup(AtkUnitBase* atkUnitBase)
    {
        if (atkUnitBase is null) return;
        
        needGreedReceiveEventHook ??= Hook<NeedGreedReceiveEventDelegate>.FromAddress((nint) atkUnitBase->AtkEventListener.vfunc[2], OnNeedGreedReceiveEvent);
        needGreedReceiveEventHook?.Enable();

        // Find first item that hasn't been rolled on, and select it.
        var addonNeedGreed = (AddonNeedGreed*) atkUnitBase;
        foreach (var index in Enumerable.Range(0, addonNeedGreed->NumItems))
        {
            if (addonNeedGreed->ItemsSpan[index] is { Roll: 0, ItemId: not 0 })
            {
                SelectItem(addonNeedGreed, index);
                break;
            }
        }
    }

    protected override void Disable()
    {
        needGreedReceiveEventHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        needGreedReceiveEventHook?.Dispose();
        base.Dispose();
    }

    private void OnNeedGreedReceiveEvent(AddonNeedGreed* addon, AtkEventType type, uint buttonType, AtkEvent* eventInfo, nint data)
    {
        needGreedReceiveEventHook!.Original(addon, type, buttonType, eventInfo, data);
        
        try
        {
            if (type is not AtkEventType.ButtonClick) return;

            switch (buttonType)
            {
                case 0: // Need
                case 1: // Greed
                case 2 when GetSelectedItem(addon) is { Roll: 0, ItemId: not 0 }: // Pass, don't select next item if we are passing on an item that we already rolled on
                    SetSelectNextItem(addon);
                    break;
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error(exception, "Something went wrong in 'Loot Window Select Next Item', let MidoriKami know.");
        }
    }

    private void SetSelectNextItem(AddonNeedGreed* addon)
    {
        var currentItemCount = addon->AtkUnitBase.AtkValues[3].UInt;
        var nextIndex = addon->SelectedItemIndex + 1;
        
        if (nextIndex == currentItemCount) nextIndex = 0;
        
        SelectItem(addon, nextIndex);
    }

    private void SelectItem(AddonNeedGreed* addon, int index)
    {
        var values = stackalloc int[6];
        values[4] = index;
        OnNeedGreedReceiveEvent(addon, AtkEventType.ListItemToggle, 0, null, (nint)values); 
    }

    private LootItemInfo GetSelectedItem(AddonNeedGreed* addon) 
        => addon->ItemsSpan[addon->SelectedItemIndex];
}