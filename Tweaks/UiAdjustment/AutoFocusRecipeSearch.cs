using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Auto Focus Recipe Search")]
[TweakDescription("Automatically focus the recipe search when opening the crafting log.")]
[TweakReleaseVersion("1.8.9.0")]
public unsafe class AutoFocusRecipeSearch : UiAdjustments.SubTweak {
    [AddonPostSetup("RecipeNote")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase == null) return;
        if (atkUnitBase->CollisionNodeList == null) {
            // Not sure why this hasn't been initialized yet, but do it ourselves.
            atkUnitBase->UpdateCollisionNodeList(false);
        }
        if (atkUnitBase->CollisionNodeList == null || atkUnitBase->CollisionNodeListCount < 20) return;
        atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[19]);
    }
}
