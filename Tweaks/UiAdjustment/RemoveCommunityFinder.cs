using System.Collections.Generic;
using Dalamud.Game.Internal;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class RemoveCommunityFinder : UiAdjustments.SubTweak {
        public override string Name => "Remove Community Finder";

        private List<string> windowsWithCommunityFinder = new List<string>() {
            "Social",
            "FreeCompany",
            "LinkShell",
            "CrossWorldLinkshell",
            "CircleFinder",
            "CircleList",
            "ContactList",
            "PvPTeam"
        };

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            foreach(var w in windowsWithCommunityFinder) UpdateCommunityFinderButton(PluginInterface.Framework, w, true);
            base.Disable();
        }

        private void OnFrameworkUpdate(Framework framework) {
            foreach (var w in windowsWithCommunityFinder) UpdateCommunityFinderButton(PluginInterface.Framework, w);
        }

        private unsafe void UpdateCommunityFinderButton(Framework framework, string name, bool reset = false) {
            var socialWindow = (AtkUnitBase*) framework.Gui.GetUiObjectByName(name, 1);
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
}
