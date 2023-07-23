using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Auto Focus Recipe Search")]
[TweakDescription("Automatically focus the recipe search when opening the crafting log.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class AutoFocusRecipeSearch : UiAdjustments.SubTweak {
    [AddonSetup("RecipeNote")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[19]);
    }
}
