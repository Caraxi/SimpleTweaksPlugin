using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Remove Community Finder")]
[TweakDescription("Hide the community finder buttons from social windows.")]
public class RemoveCommunityFinder : UiAdjustments.SubTweak {
    [AddonPostSetup("Social", "FreeCompany", "LinkShell", "CrossWorldLinkshell", "CircleFinder", "CircleList", "CircleBook", "ContactList", "PvPTeam")]
    private unsafe void OnAddonSetup(AtkUnitBase* addon) {
        try {
            if (addon == null) return;
            var node = addon->RootNode;
            if (node == null) return;
            node = node->ChildNode;
            if (node == null) return;
            while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;
            node->ToggleVisibility(false);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
}
