using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Auto Focus Market Board Item Search")]
[TweakDescription("Automatically focus the item search when opening the market board.")]
[TweakAuthor("croizat")]
[TweakReleaseVersion("1.10.3.0")]
public unsafe class AutoFocusMarketboardSearch : UiAdjustments.SubTweak {
    [AddonPostSetup("ItemSearch")]
    private void AddonSetup(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase == null) return;
        if (atkUnitBase->CollisionNodeList == null) atkUnitBase->UpdateCollisionNodeList(false);
        if (atkUnitBase->CollisionNodeList == null || atkUnitBase->CollisionNodeListCount < 12) return;
        atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[11]);
    }
}
