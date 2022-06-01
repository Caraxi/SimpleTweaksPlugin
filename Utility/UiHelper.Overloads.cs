using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Utility; 

public static unsafe partial class UiHelper {
    public static void SetSize<T>(T* node, int? w, int? h) where T : unmanaged => SetSize((AtkResNode*) node, w, h);
    public static void SetPosition<T>(T* node, float? x, float? y) where T : unmanaged => SetPosition((AtkResNode*) node, x, y);
    public static T* CloneNode<T>(T* original) where T : unmanaged => (T*) CloneNode((AtkResNode*) original);
}