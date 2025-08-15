using Dalamud;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Enable chat bubbles in combat")]
[TweakDescription("Allow chat bubbles to be displayed while in combat.")]
[TweakReleaseVersion(UnreleasedVersion)]
public class CombatChatBubbles : Tweak {
    [Signature("?? 0F 84 ?? ?? ?? ?? F6 80 ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 49 8B 06")]
    private nint addr;
    protected override void Enable() => SafeMemory.WriteBytes(addr + 1, [0x90, 0xE9]);
    protected override void Disable() => SafeMemory.WriteBytes(addr + 1, [0x0F, 0x84]);
}
