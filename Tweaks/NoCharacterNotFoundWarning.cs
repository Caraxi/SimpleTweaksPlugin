using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Hide 'Character not found' Message")]
[TweakDescription("Prevent the game from displaying the \"The character you last logged out with could not be found on the current data center.\" message.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class NoCharacterNotFoundWarning : Tweak {
    protected override void Enable() => AgentLobby.Instance()->HasShownCharacterNotFound = true;
}
