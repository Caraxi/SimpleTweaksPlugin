using Dalamud.Game.ClientState.Keys;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public class DisableLeftClickCameraControl : Tweak {
    public override string Name => "Disable Left Click Camera Control";
    public override string Description => "Disable all control of the camera using left click.";

    public enum CameraControlType : ulong { None, Keyboard, Gamepad, Mouse }
    private delegate CameraControlType GetCameraControlType();
    private HookWrapper<GetCameraControlType> getCameraControlTypeHook;

    public class Configs : TweakConfig {
        [TweakConfigOption("Allow left click camera movement while holding SHIFT")]
        public bool AllowWithShift = true;
    }

    public Configs Config { get; private set; }
    public override bool UseAutoConfig => true;

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        getCameraControlTypeHook ??= Common.Hook<GetCameraControlType>("E8 ?? ?? ?? ?? 83 F8 01 74 55", GetCameraControlTypeDetour);
        getCameraControlTypeHook?.Enable();
        base.Enable();
    }

    private CameraControlType GetCameraControlTypeDetour() {
        var cameraControlType = getCameraControlTypeHook.Original();
        if (Config.AllowWithShift && Service.KeyState[VirtualKey.SHIFT]) return cameraControlType ;
        if (Service.KeyState[VirtualKey.LBUTTON]) return CameraControlType.None;
        return cameraControlType;
    }

    protected override void Disable() {
        getCameraControlTypeHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        getCameraControlTypeHook?.Dispose();
        base.Dispose();
    }
}
