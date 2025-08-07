using System.Runtime.InteropServices;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Customize Group Pose Camera Control")]
[TweakDescription("Allows you to customize the camera control in group pose")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.4.0")]
[TweakCategory(TweakCategory.QoL)]
public unsafe class CustomizeGroupPoseCameraControl : Tweak {
    public class Configs : TweakConfig {
        public ModifierFlag PanModifier = ModifierFlag.Alt;
        public bool PanInvert;
    }

    [TweakConfig] public Configs TweakConfig { get; set; }

    protected void DrawConfig() {
        ImGui.Text("Pan Control:");
        using (ImRaii.PushIndent()) {
            if (ImGui.Checkbox("Make pre 7.1 the default", ref TweakConfig.PanInvert)) {
                if (!TweakConfig.PanInvert && TweakConfig.PanModifier == 0) {
                    TweakConfig.PanModifier = ModifierFlag.Alt;
                }
            }

            ImGuiExt.ModifierFlagEditor(ref TweakConfig.PanModifier, TweakConfig.PanInvert);

            if (TweakConfig.PanInvert) {
                if (TweakConfig.PanModifier == 0) {
                    ImGui.TextDisabled("Modifier keys are disabled. Tweak is set to change default pan mode to pre-7,1 style with no keybind for post-7.1");
                } else {
                    ImGui.TextDisabled($"If the modifier key(s) [{TweakConfig.PanModifier}] are held, the group pose camera will act as it does after 7.1");
                }
            } else {
                ImGui.TextDisabled($"If the modifier key(s) [{TweakConfig.PanModifier}] are held, the group pose camera will act as it did before 7.1");
            }
        }
    }

    private delegate void* CameraUpdate(CameraStruct* camera);

    [TweakHook, Signature("E8 ?? ?? ?? ?? F3 0F 10 83 ?? ?? ?? ?? 41 0F 2E C1", DetourName = nameof(CameraUpdateDetour))]
    private HookWrapper<CameraUpdate> cameraUpdateHook;

    [StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
    private struct CameraStruct {
        [FieldOffset(0x160)] public float Rotation;
    }

    private void* CameraUpdateDetour(CameraStruct* camera) {
        if (!Service.ClientState.IsGPosing) return cameraUpdateHook.Original(camera);
        if (TweakConfig == null) return cameraUpdateHook.Original(camera);

        var doPan = (TweakConfig.PanInvert && TweakConfig.PanModifier == 0) || TweakConfig.PanModifier.IsPressed() != TweakConfig.PanInvert;

        var rotation = camera->Rotation;
        try {
            if (doPan) camera->Rotation = 0;
            return cameraUpdateHook.Original(camera);
        } finally {
            if (doPan) camera->Rotation += rotation;
        }
    }
}
