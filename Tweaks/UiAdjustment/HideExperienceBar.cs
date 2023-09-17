using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide Experience Bar at Max Level")]
[TweakDescription("Hides the experience bar when at max level.")]
[TweakAuthor("Anna")]
public unsafe class HideExperienceBar : UiAdjustments.SubTweak {
    [AddonPostUpdate("_Exp")]
    private void UpdateExp(AtkUnitBase* addonExp) {
        if (addonExp == null) return;
        var node = addonExp->GetTextNodeById(4);
        if (node == null) return;
        SetExperienceBarVisible(!node->NodeText.GetSeString().TextValue.Contains("-/-"));
    }

    private static void SetExperienceBarVisible(bool visible) {
        if (!Common.GetUnitBase("_Exp", out var expAddon)) return;
        expAddon->IsVisible = visible;
    }

    [FrameworkUpdate(NthTick = 300)] 
    protected override void Enable() => UpdateExp(Common.GetUnitBase("_Exp"));
    protected override void Disable() => SetExperienceBarVisible(true);
}