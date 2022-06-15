using System;
using System.Collections.Generic;
using Dalamud.Game;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public class RemoveCommunityFinder : UiAdjustments.SubTweak {
    public override string Name => "Remove Community Finder";
    public override string Description => "Hide the community finder buttons from social windows.";

    private List<string> windowsWithCommunityFinder = new List<string>() {
        "Social",
        "FreeCompany",
        "LinkShell",
        "CrossWorldLinkshell",
        "CircleFinder",
        "CircleList",
        "CircleBook",
        "ContactList",
        "PvPTeam"
    };

    public override void Enable() {
        Service.Framework.Update += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable() {
        Service.Framework.Update -= OnFrameworkUpdate;
        foreach(var w in windowsWithCommunityFinder) UpdateCommunityFinderButton(Service.Framework, w, true);
        base.Disable();
    }

    private void OnFrameworkUpdate(Framework framework) {
        try {
            foreach (var w in windowsWithCommunityFinder) UpdateCommunityFinderButton(Service.Framework, w);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private unsafe void UpdateCommunityFinderButton(Framework framework, string name, bool reset = false) {
        var socialWindow = Common.GetUnitBase(name);
        if (socialWindow == null) return;
        var node = socialWindow->RootNode;
        if (node == null) return;
        node = node->ChildNode;
        if (node == null) return;
        while (node->PrevSiblingNode != null) {
            // Get the last sibling in the tree
            node = node->PrevSiblingNode;
        }
        if (reset) node->Flags |= 0x10;
        else node->Flags &= ~0x10;
    }
}