﻿using System;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Timer on Duty Waiting")]
[TweakDescription("Shows the 45 second countdown after readying for a duty.")]
public unsafe class TimerOnDutyWaiting : UiAdjustments.SubTweak {
    private readonly Utf8String* timeRemainingString = Utf8String.CreateEmpty();
    private readonly string prefix = Service.Data.GetExcelSheet<Addon>().GetRow(2780).Text.ExtractText();
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

        timeRemainingString->SetString($"{prefix} ({timeRemaining})");
        addon->GetTextNodeById(3)->SetText(timeRemainingString->StringPtr);
    }
}
