using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class DisableMountVolumeChange : Tweak {
    public override string Name => "Disable Mount Music Volume Change";
    public override string Description => "Prevents mount music from going quiet when not moving.";
    protected override string Author => "perchbird";
    
    private delegate int GetSpecialMode(void* unused, byte specialModeType);
    private HookWrapper<GetSpecialMode> getSpecialModeHook;

    public override void Enable() {
        getSpecialModeHook ??= Common.Hook<GetSpecialMode>("48 89 5C 24 ?? 57 48 83 EC 20 8B 41 10 33 DB", GetSpecialModeDetour);
        getSpecialModeHook?.Enable();
        base.Enable();
    }

    private int GetSpecialModeDetour(void* unused, byte specialModeType)
    {
        return specialModeType == 2 ? 4 : getSpecialModeHook.Original(unused, specialModeType);
    }

    public override void Disable() {
        getSpecialModeHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        getSpecialModeHook?.Dispose();
        base.Dispose();
    }
}