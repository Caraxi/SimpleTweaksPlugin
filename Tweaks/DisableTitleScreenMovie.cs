using Dalamud;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

internal class DisableTitleScreenMovie : Tweak {
    public override string Name => "Disable Title Screen Movie";
    public override string Description => "Prevents the title screen from playing the introduction movie after 60 seconds.";

    private nint compareIdleTimeAddress;

    protected override void Enable() {
        if (!Service.SigScanner.TryScanText("48 81 ?? ?? ?? ?? ?? 60 EA 00 00 0F 86 ?? ?? ?? ??", out compareIdleTimeAddress)) return;
        compareIdleTimeAddress += 11;
        SafeMemory.Write(compareIdleTimeAddress, new byte[]{0x90, 0xE9});
    }

    protected override void Disable() {
        if (compareIdleTimeAddress == nint.Zero) return;
        if (SafeMemory.Write(compareIdleTimeAddress, new byte[]{0x0F, 0x86}))
            compareIdleTimeAddress = nint.Zero;
    }
}
