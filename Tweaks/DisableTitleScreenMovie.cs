using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Disable Title Screen Movie")]
[TweakDescription("Prevents the title screen from playing the introduction movie after 60 seconds.")]
[TweakCategory(TweakCategory.QoL)]
internal unsafe class DisableTitleScreenMovie : Tweak {
    [FrameworkUpdate] private void Update() => AgentLobby.Instance()->IdleTime = 0;
}