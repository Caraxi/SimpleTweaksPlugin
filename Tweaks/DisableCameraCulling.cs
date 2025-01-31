using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Disable Camera Culling")]
[TweakDescription("Disable the hiding of characters when the camera gets too close.")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class DisableCameraCulling : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Disable culling in Group Pose")] public bool DisableInGroupPose = true;
        [TweakConfigOption("Disable culling outside Group Pose")] public bool DisableOutsideGroupPose;
    }
    [TweakConfig] public Configs Config { get; private set; }
    
    [TweakHook(typeof(CameraBase), nameof(CameraBase.ShouldDrawGameObject), nameof(ShouldDrawGameObjectDetour))]
    private HookWrapper<CameraBase.Delegates.ShouldDrawGameObject> shouldDrawGameObjectHook = null!;

    public bool ShouldDrawGameObjectDetour(CameraBase* thisPtr, GameObject* gameObject, Vector3* sceneCameraPos, Vector3* lookAtVector) {
        switch (Service.ClientState.IsGPosing) {
            case true when Config.DisableInGroupPose:
            case false when Config.DisableOutsideGroupPose: return true;
            default: return shouldDrawGameObjectHook.Original(thisPtr, gameObject, sceneCameraPos, lookAtVector);
        }
    }
}
