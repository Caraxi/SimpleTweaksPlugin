using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Auto Focus Marketboard Item Search")]
[TweakDescription("Automatically focus the item search when opening the marketboard.")]
[TweakAuthor("croizat")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class AutoFocusMarketboardSearch : UiAdjustments.SubTweak {
    [AddonPostSetup("ItemSearch")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase == null) return;
        if (atkUnitBase->CollisionNodeList == null) {
            // Not sure why this hasn't been initialized yet, but do it ourselves.
            atkUnitBase->UpdateCollisionNodeList(false);
        }
        if (atkUnitBase->CollisionNodeList == null) return;
        atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[11]);
    }
}
