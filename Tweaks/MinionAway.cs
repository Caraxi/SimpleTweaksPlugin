using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Dismiss Minion Command")]
[TweakDescription($"Adds a command to dismiss your current minion.")]
[TweakReleaseVersion("1.8.2.0")]
public unsafe class MinionAway : CommandTweak {
    protected override void OnCommand(string args) {
        var c = (Character*)(Service.Objects.LocalPlayer?.Address ?? nint.Zero);
        if (c == null) return;
        var minion = c->CompanionData.CompanionObject;
        if (minion == null) return;
        if (minion->Character.BaseId == 0) return;
        ActionManager.Instance()->UseAction((ActionType)8, minion->Character.BaseId);
    }

    protected override string Command => "minionaway";
}
