using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Open Commendation Window Automatically")]
[TweakDescription("Open the commendation window upon completion of a duty.")]
public unsafe class AutoOpenCommendWindow : Tweak {
    private bool hasOpenedMvp;
    private byte throttle;

    [FrameworkUpdate(NthTick = 10)]
    private void FrameworkOnUpdate() {
        throttle++;

        if (Service.Condition[ConditionFlag.WatchingCutscene] || Service.Condition[ConditionFlag.WatchingCutscene78] || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) {
            hasOpenedMvp = false;
            throttle = 0;
        }

        if (throttle <= 2) return;

        throttle = 0;

        if (Common.GetUnitBase("_NotificationIcMvp") == null) {
            hasOpenedMvp = false;
        } else {
            if (hasOpenedMvp) return;
            hasOpenedMvp = true;
            TryOpenWindow();
        }
    }

    private static void TryOpenWindow() {
        SimpleLog.Verbose("Try opening VoteMvp");
        var needGreedWindow = Service.GameGui.GetAddonByName("VoteMvp");
        if (needGreedWindow != IntPtr.Zero) {
            SimpleLog.Verbose("VoteMvp already open.");
            return;
        }

        SimpleLog.Verbose("Opening VoteMvp window.");
        var notification = Common.GetUnitBase("_Notification");
        if (notification == null) {
            SimpleLog.Verbose("_Notification not open.");
            return;
        }

        Common.GenerateCallback(notification, 0, 11);
    }
}
