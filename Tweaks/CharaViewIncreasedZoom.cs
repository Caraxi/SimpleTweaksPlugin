using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Increased zoom on character previews")]
[TweakDescription("Allows zooming in near infinitely on character preview views, such as Try On and Examine.")]
public unsafe class CharaViewIncreasedZoom : Tweak {
    private delegate void CharaViewZoom(CharaView* charaView, float delta);

    [TweakHook, Signature("48 8B 41 20 48 85 C0 74 4C", DetourName = nameof(ZoomDetour))]
    private HookWrapper<CharaViewZoom> charaViewZoomHook;

    [StructLayout(LayoutKind.Explicit)]
    private struct Camera {
        [FieldOffset(0x1F8)] public float Zoom;
        [FieldOffset(0xE0)] public Camera* AnotherCamera;
    }

    private const float MinZoom = float.Epsilon;
    private const float MaxZoom = 5f;

    private void ZoomDetour(CharaView* charaView, float delta) {
        var camera = (Camera*)charaView->Camera;
        if (camera == null || camera->AnotherCamera == null) return;
        camera->AnotherCamera->Zoom += delta * (camera->AnotherCamera->Zoom / 24);
        if (camera->AnotherCamera->Zoom < MinZoom) camera->AnotherCamera->Zoom = MinZoom;
        if (camera->AnotherCamera->Zoom > MaxZoom) camera->AnotherCamera->Zoom = MaxZoom;
    }
}
