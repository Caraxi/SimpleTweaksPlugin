using System;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Housing Lottery Timer")]
[TweakDescription("Show the time remaining until the current lottery period ends in the timers window.")]
[TweakReleaseVersion("1.8.9.0")]
public unsafe class HouseLotteryTimer : UiAdjustments.SubTweak {
    private readonly DateTime scheduleStartTime = new(2022, 05, 26, 15, 0, 0);
    private const bool FirstPeriodEntry = true;
    private const int EntryPeriodDays = 5;
    private const int ResultsPeriodDays = 4;

    private record LotteryTimeInfo(DateTime PeriodBegan, DateTime PeriodEnds, bool IsEntryPeriod) {
        public TimeSpan NextPeriodBegins => PeriodEnds.Subtract(DateTime.UtcNow);
    }

    private LotteryTimeInfo GetLotteryTimeInfo(DateTime? forUtcDate = null) {
        forUtcDate ??= DateTime.UtcNow;
        var isEntryPeriod = FirstPeriodEntry;
        var currentPeriodStarted = new DateTime(scheduleStartTime.Ticks);
        var nextPeriodBegins = new DateTime(scheduleStartTime.Ticks) + TimeSpan.FromDays(EntryPeriodDays);
        while (nextPeriodBegins < forUtcDate) {
            currentPeriodStarted = nextPeriodBegins;
            isEntryPeriod = !isEntryPeriod;
            nextPeriodBegins = currentPeriodStarted + TimeSpan.FromDays(isEntryPeriod ? EntryPeriodDays : ResultsPeriodDays);
        }

        return new LotteryTimeInfo(currentPeriodStarted, nextPeriodBegins, isEntryPeriod);
    }

    [AddonPreRefresh("ContentsInfo")]
    private void AddonRefresh(AddonRefreshArgs refreshArgs) {
        try {
            var atkValue = (AtkValue*)refreshArgs.AtkValues;
            for (var i = 0; i < refreshArgs.AtkValueCount; i++, atkValue++) {
                if (atkValue->Type != ValueType.Int) continue;
                if (atkValue->Int != 18) continue;
                atkValue++;
                if (atkValue->String == null || atkValue->Type != ValueType.String8) break;
                var lotteryPeriod = GetLotteryTimeInfo();
                var displayString = Common.ReadString(atkValue->String);
                if (string.IsNullOrEmpty(displayString)) break;
                var timespan = lotteryPeriod.NextPeriodBegins;
                displayString += "  ";
                displayString += $"{(int)timespan.TotalHours}:{timespan.Minutes:00}";
                atkValue->SetManagedString(displayString);
                break;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
}
