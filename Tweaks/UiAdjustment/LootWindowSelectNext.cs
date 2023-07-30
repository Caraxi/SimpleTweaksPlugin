#nullable enable
using System;
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
            // ButtonType 3 and 0 are for "Loot Recipient" and "Greed Only"
            if (type is AtkEventType.ButtonClick && buttonType is not 3 and not 0)
            {
                var currentSelectedItem = *((byte*) addon + 0x508);
                var currentItemCount = addon->AtkUnitBase.AtkValues[3].UInt;
                var nextIndex = currentSelectedItem + 1;

                if (nextIndex == currentItemCount) nextIndex = 0;

                var values = stackalloc int[6];
                values[4] = nextIndex;
                OnNeedGreedReceiveEvent(addon, AtkEventType.ListItemToggle, 0, null, (nint)values);
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error(exception, "Something went wrong in 'Loot Window Select Next Item', let MidoriKami know.");
        }
    }
}