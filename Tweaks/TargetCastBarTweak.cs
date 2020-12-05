using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Internal;
using Dalamud.Game.Internal.Gui.Structs;
using Dalamud.Plugin;
using Addon = Dalamud.Game.Internal.Gui.Addon.Addon;
using AddonStruct = Dalamud.Game.Internal.Gui.Structs.Addon;

namespace SimpleTweaksPlugin.Tweaks {
    class TargetCastBarTweak : Tweak {
        public override string Name => "Reposition Target Castbar Text";

        public override bool Experimental => true;

        public override void Setup() {
            Ready = true;
        }

        public void OnFrameworkUpdate(Framework framework) {
            HandleBars(framework);
        }

        private void HandleBars(Framework framework, bool reset = false) {
            var seperatedCastBar = framework.Gui.GetAddonByName("_TargetInfoCastBar", 1);
            if (seperatedCastBar != null && (seperatedCastBar.Visible || reset)) {
                HandleSeperatedCastBar(seperatedCastBar, reset);
                if (!reset) return;
            }

            var mainTargetInfo = framework.Gui.GetAddonByName("_TargetInfo", 1);
            if (mainTargetInfo != null && (mainTargetInfo.Visible || reset)) {
                HandleMainTargetInfo(mainTargetInfo, reset);
            }
        }

        private unsafe void HandleSeperatedCastBar(Addon addon, bool reset = false) {
            var addonStruct = Marshal.PtrToStructure<AddonStruct>(addon.Address);
            if (addonStruct.RootNode == null) return;
            var rootNode = addonStruct.RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            DoShift(child, reset);
        }

        private unsafe void HandleMainTargetInfo(Addon addon, bool reset = false) {
            var addonStruct = Marshal.PtrToStructure<AddonStruct>(addon.Address);
            if (addonStruct.RootNode == null) return;
            var rootNode = addonStruct.RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            for (var i = 0; i < 8; i++) {
                if (child->PrevSiblingNode == null) return;
                child = child->PrevSiblingNode;
            }

            DoShift(child, reset);
        }

        private unsafe void DoShift(AtkResNode* node, bool reset = false) {
            if (node->ChildCount != 5) return; // Should have 5 children
            if (node->ChildNode == null) return;
            node = node->ChildNode;
            if (node->PrevSiblingNode == null) return;
            node = node->PrevSiblingNode;
            if (node->PrevSiblingNode == null) return;
            node = node->PrevSiblingNode;
            var skillTextNode = node->PrevSiblingNode; // 4th Child
            Marshal.WriteInt16(new IntPtr(skillTextNode), 0x92, reset ? (short) 24 : (short) 8);
        }

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            Enabled = true;
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            PluginLog.Log("Resetting");
            HandleBars(PluginInterface.Framework, true);
            Enabled = false;
        }

        public override void Dispose() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            Enabled = false;
            Ready = false;
        }
    }
}
