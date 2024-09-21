using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Disable Mount Music Volume Change")]
[TweakAuthor("perchbird")]
[TweakDescription("Prevents mount music from going quiet when not moving.")]
[TweakCategory(TweakCategory.QoL)]
public unsafe class DisableMountVolumeChange : Tweak {
    
    private delegate void GetSpecialMode(byte* bgmPlayer);
    private delegate void UpdateFromSpecialMode(byte* a1, int isMountLoud);
    private HookWrapper<GetSpecialMode> getSpecialModeHook;
    private HookWrapper<UpdateFromSpecialMode> updateFromSpecialModeHook;

    private int specialModeType;

    protected override void Enable() {
        getSpecialModeHook ??= Common.Hook<GetSpecialMode>("40 57 48 83 EC 20 48 83 79 ?? ?? 48 8B F9 0F 84 ?? ?? ?? ?? 0F B6 51 4D", GetSpecialModeDetour);
        updateFromSpecialModeHook ??= Common.Hook<UpdateFromSpecialMode>("8B 81 ?? ?? ?? ?? 85 C0 74 05 83 F8 02", UpdateFromSpecialModeDetour);
        getSpecialModeHook?.Enable();
        updateFromSpecialModeHook?.Enable();
        base.Enable();
    }

    private void GetSpecialModeDetour(byte* bgmPlayer)
    {
        specialModeType = *(bgmPlayer + 0x4D);
        getSpecialModeHook.Original(bgmPlayer);
    }
    
    private void UpdateFromSpecialModeDetour(byte* a1, int isMountLoud)
    {
        if (specialModeType != 2)
        {
            updateFromSpecialModeHook.Original(a1, isMountLoud);
            return;
        }

        updateFromSpecialModeHook.Original(a1, 1);
    }

    protected override void Disable() {
        getSpecialModeHook?.Disable();
        updateFromSpecialModeHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        getSpecialModeHook?.Dispose();
        updateFromSpecialModeHook?.Dispose();
        base.Dispose();
    }
}