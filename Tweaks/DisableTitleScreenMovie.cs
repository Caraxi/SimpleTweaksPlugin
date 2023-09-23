using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

internal unsafe class DisableTitleScreenMovie : Tweak {
    public override string Name => "Disable Title Screen Movie";
    public override string Description => "Prevents the title screen from playing the introduction movie after 60 seconds.";

    protected override void Enable() {
        Common.FrameworkUpdate += FrameworkUpdate;
        base.Enable();
    }

    protected override void Disable() {
        Common.FrameworkUpdate -= FrameworkUpdate;
        base.Disable();
    }

    private void FrameworkUpdate() {
        try {
            if (Service.Condition == null) return;
            if (Service.Condition.Any()) return;
            AgentLobby.Instance()->IdleTime = 0;
        } catch {
            // Ignored
        }
    }
}