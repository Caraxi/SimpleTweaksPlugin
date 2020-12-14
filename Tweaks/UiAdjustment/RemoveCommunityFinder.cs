using Dalamud.Game.Internal;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class RemoveCommunityFinder : UiAdjustments.SubTweak {
        public override string Name => "Remove Community Finder";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            UpdateCommunityFinderButton(PluginInterface.Framework, "Social", true);
            UpdateCommunityFinderButton(PluginInterface.Framework, "FreeCompany", true);
            base.Disable();
        }

        private void OnFrameworkUpdate(Framework framework) { 
            UpdateCommunityFinderButton(framework, "Social");
            UpdateCommunityFinderButton(framework, "FreeCompany");
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
