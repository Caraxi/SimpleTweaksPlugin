using System;
using System.Diagnostics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Timer on Duty Waiting")]
[TweakDescription("Shows the 45 second countdown after readying for a duty.")]
[Changelog("1.10.5.0", "Fixed display of text in french clients.")]
public unsafe class TimerOnDutyWaiting : UiAdjustments.SubTweak {
    private readonly Utf8String* timeRemainingString = Utf8String.CreateEmpty();
    private readonly SeString prefix = Service.Data.GetExcelSheet<Addon>().GetRow(2780).Text.ToDalamudString();
    private Stopwatch stopwatch = new();
    private TimeSpan timeOffset = TimeSpan.Zero;

    public override void Dispose() {
        timeRemainingString->Dtor(true);
        base.Dispose();
    }

    [AddonPostRequestedUpdate("ContentsFinderConfirm")]
    private void OnPostSetup(AtkUnitBase* addon) {
        if (TimeSpan.TryParse($"0:{addon->GetTextNodeById(60)->NodeText.ToString()}", out var timeSpan)) {
            timeOffset = timeSpan;
            stopwatch = Stopwatch.StartNew();
        }
    }

    [AddonPostUpdate("ContentsFinderReady")]
    private void OnPostUpdate(AtkUnitBase* addon) {
        var timeRemaining = timeOffset.Seconds - stopwatch.Elapsed.Seconds;
        if (timeRemaining <= 0) return;

        var str = new SeString(prefix.Payloads);
        str.Payloads.Add(new TextPayload($" ({timeRemaining})"));
        
        timeRemainingString->SetString(str.EncodeWithNullTerminator());
        addon->GetTextNodeById(3)->SetText(timeRemainingString->StringPtr);
    }
}
