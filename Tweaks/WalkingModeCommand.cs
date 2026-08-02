using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Walking Mode Command")]
[TweakDescription("Adds a command to toggle walking mode")]
[TweakAuthor("SubaruYashiro")]
public unsafe class WalkingModeCommand: CommandTweak
{
    protected override string Command => "/walk";
    protected override string HelpMessage => "Toggle Walking Mode";

    protected override void OnCommand(string args)
    {
        Control* ctrl = Control.Instance();
        ctrl->IsWalking = !ctrl->IsWalking;
    }
}
