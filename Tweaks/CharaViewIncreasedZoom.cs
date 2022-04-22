using System.Runtime.InteropServices;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class CharaViewIncreasedZoom : Tweak {
    public override string Name => "Increased zoom on character previews.";
    public override string Description => "Allows zooming in near infinitely on character preview views, such as Try On and Examine.";

    private delegate void CharaViewZoom(CharaView* charaView, float delta);
    private HookWrapper<CharaViewZoom> charaViewZoomHook;

    public override void Enable() {
        charaViewZoomHook ??= Common.Hook<CharaViewZoom>("48 8B 41 20 48 85 C0 74 4C", ZoomDetour);
        charaViewZoomHook?.Enable();
        base.Enable();
    }


    [StructLayout(LayoutKind.Explicit, Size = 0xC8)]
    public struct CharaView {
        [FieldOffset(0x20)] public Camera* Camera;
        [FieldOffset(0xC4)] public float ZoomRatio;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xEF)]
    public struct Camera {
        [FieldOffset(0xB4)] public float Zoom;
        [FieldOffset(0xE0)] public Camera* AnotherCamera;
    }

    private const float MinZoom = float.Epsilon;
    private const float MaxZoom = 5f;

    private void ZoomDetour(CharaView* charaView, float delta) {
        if (charaView->Camera != null && charaView->Camera->AnotherCamera != null) {
            charaView->Camera->AnotherCamera->Zoom += delta * (charaView->Camera->AnotherCamera->Zoom / 4);
            if (charaView->Camera->AnotherCamera->Zoom < MinZoom) charaView->Camera->AnotherCamera->Zoom = MinZoom;
            if (charaView->Camera->AnotherCamera->Zoom > MaxZoom) charaView->Camera->AnotherCamera->Zoom = MaxZoom;
        }
    }

    public override void Disable() {
        charaViewZoomHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        charaViewZoomHook?.Dispose();
        base.Dispose();
    }
}