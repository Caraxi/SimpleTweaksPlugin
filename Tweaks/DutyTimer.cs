using System;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Duty Timer")]
[TweakDescription("When completing a duty, tells you how much time the duty took.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion(UnreleasedVersion)]
public class DutyTimer : Tweak {
    private DateTime startTimestamp;

    protected override void Enable() {
        Service.DutyState.DutyStarted += OnDutyStarted;
        Service.DutyState.DutyCompleted += OnDutyCompleted;
    }

    protected override void Disable() {
        Service.DutyState.DutyStarted -= OnDutyStarted;
        Service.DutyState.DutyCompleted -= OnDutyCompleted;
    }
    
    private void OnDutyStarted(object sender, ushort e) 
        => startTimestamp = DateTime.UtcNow;

    private void OnDutyCompleted(object sender, ushort e) 
        => Service.Chat.Print($@"Duty Completed in: {DateTime.UtcNow - startTimestamp:hh\:mm\:ss\.ffff}");
}