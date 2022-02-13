using System;
using System.Diagnostics;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class TimerOnDutyWaiting : UiAdjustments.SubTweak {
    public override string Name => "Timer on Duty Waiting";
    public override string Description => "Shows the 45 second countdown after readying for a duty.";

    public override void Enable() {
        Service.Framework.Update += UpdateFramework;
        prefix = Service.Data.Excel.GetSheet<Addon>()?.GetRow(2780)?.Text?.RawString ?? "Checking member status...";
        base.Enable();
    }

    private string prefix = "Checking member status...";

    private int lastValue;

    private readonly Stopwatch sw = new();

    private void UpdateFramework(Framework framework) {
        try {
            var confirmWindow = Common.GetUnitBase("ContentsFinderConfirm");
            if (confirmWindow != null && confirmWindow->UldManager.NodeList != null) {
                var timerTextNode = (AtkTextNode*) confirmWindow->UldManager.NodeList[10];
                if (timerTextNode == null) return;
                var text = Plugin.Common.ReadSeString(timerTextNode->NodeText.StringPtr).TextValue;
                if (!TimeSpan.TryParse($"0:{text}", out var ts)) return;
                if (!sw.IsRunning || (int)ts.TotalSeconds != lastValue) {
                    lastValue = (int)ts.TotalSeconds;
                    sw.Restart();
                }

                return;
            }

            if (!sw.IsRunning) return;

            var v = lastValue - (int)(sw.Elapsed.TotalSeconds + 1);
            if (v < 0) {
                sw.Stop();
                return;
            }

            var readyWindow = Common.GetUnitBase("ContentsFinderReady");
            if (readyWindow == null || readyWindow->UldManager.NodeList == null) return;

            var checkingTextNode = (AtkTextNode*) readyWindow->UldManager.NodeList[7];
            if (checkingTextNode == null) return;

            Plugin.Common.WriteSeString(checkingTextNode->NodeText, $"{prefix} ({v})");
        } catch {
            //
        }
    }

    public override void Disable() {
        Service.Framework.Update -= UpdateFramework;
        sw.Stop();
        base.Disable();
    }
}