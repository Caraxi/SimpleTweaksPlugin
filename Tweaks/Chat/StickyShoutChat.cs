using Dalamud;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Sticky Shout Chat")]
[TweakDescription("Prevents the game from automatically switching out of shout chat.")]
[TweakReleaseVersion("1.9.6.0")]
public class StickyShoutChat : ChatTweaks.SubTweak {
    private nint editAddress;

    protected override void Enable() {
        if (!Service.SigScanner.TryScanText("05 75 0C 8B D3 E8 ?? ?? ?? ?? E9", out editAddress)) return;
        SafeMemory.Write(editAddress, (sbyte)-2);
    }

    protected override void Disable() {
        if (editAddress == nint.Zero) return;
        if (SafeMemory.Write(editAddress, (sbyte)5))
            editAddress = nint.Zero;
    }
}
