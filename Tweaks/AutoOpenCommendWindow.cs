using System;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class AutoOpenCommendWindow : Tweak {
    public override string Name => "Open commendation window automatically";

    public override string Description => "Open the commendation window upon completion of a duty.";

    public override void Enable() {
        Service.Framework.Update += FrameworkOnUpdate;
        base.Enable();
    }

    private bool hasOpenedMvp;
    private byte throttle;
    private void FrameworkOnUpdate(Framework framework) {
        throttle++;

        if (Service.Condition[ConditionFlag.WatchingCutscene] ||
            Service.Condition[ConditionFlag.WatchingCutscene78] ||
            Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) {
            hasOpenedMvp = false;
            throttle = 0;
        }

        if (throttle > 10) {
            throttle = 0;
            if (Common.GetUnitBase("_NotificationIcMvp") == null) {
                if (hasOpenedMvp) {
                    hasOpenedMvp = false;
                }
            } else {
                if (hasOpenedMvp) return;
                hasOpenedMvp = true;
                TryOpenWindow();
            }
        }
    }

    private static void TryOpenWindow() {
        SimpleLog.Verbose("Try opening VoteMvp");
        var needGreedWindow = Service.GameGui.GetAddonByName("VoteMvp", 1);
        if (needGreedWindow != IntPtr.Zero) {
            SimpleLog.Verbose("VoteMvp already open.");
            return;
        }

        SimpleLog.Verbose("Opening VoteMvp window.");
        var notification = (AtkUnitBase*)Service.GameGui.GetAddonByName("_Notification", 1);
        if (notification== null) {
            SimpleLog.Verbose("_Notification not open.");
            return;
        }

        Common.GenerateCallback(notification, 0, 11);
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        base.Disable();
    }
}