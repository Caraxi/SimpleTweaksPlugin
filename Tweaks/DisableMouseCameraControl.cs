using Dalamud.Game.ClientState.Keys;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Disable Mouse Camera Control")]
[TweakDescription("Disables all control of the camera using the mouse.")]
[TweakAutoConfig]
public class DisableMouseCameraControl : Tweak {
    public enum CameraControlType { None, Keyboard, Gamepad, Mouse }

    private delegate CameraControlType GetCameraControlType();

    [TweakHook, Signature("E8 ?? ?? ?? ?? 83 F8 01 74 55", DetourName = nameof(GetCameraControlTypeDetour))]
    private HookWrapper<GetCameraControlType> getCameraControlTypeHook;

    public class Configs : TweakConfig {
        [TweakConfigOption("Allow control while holding Shift")]
        public bool AllowWithShift = true;
    }

    [TweakConfig] public Configs Config { get; private set; }

    private CameraControlType GetCameraControlTypeDetour() {
        var cameraControlType = getCameraControlTypeHook.Original();
        if (Config.AllowWithShift && Service.KeyState[VirtualKey.SHIFT]) return cameraControlType;
        return cameraControlType == CameraControlType.Mouse ? CameraControlType.None : cameraControlType;
    }
}
