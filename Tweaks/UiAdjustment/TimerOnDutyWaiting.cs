using System;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Timer on Duty Waiting")]
[TweakDescription("Shows the 45 second countdown after readying for a duty.")]
[Changelog(UnreleasedVersion, "Rewrote tweak to modernize and simplify code.")]
public unsafe class TimerOnDutyWaiting : UiAdjustments.SubTweak {
    private readonly string prefix = Service.Data.GetExcelSheet<Addon>()?.GetRow(2780)?.Text?.RawString ?? "Checking member status...";
    private Stopwatch stopwatch = new();
    private TimeSpan timeOffset = TimeSpan.Zero;

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

        addon->GetTextNodeById(3)->SetText($"{prefix} ({timeRemaining})");
    }
}