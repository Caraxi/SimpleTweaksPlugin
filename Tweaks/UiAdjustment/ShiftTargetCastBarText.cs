using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;
using Addon = Dalamud.Game.Internal.Gui.Addon.Addon;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public ShiftTargetCastBarText.Config ShiftTargetCastBarText = new ShiftTargetCastBarText.Config();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class ShiftTargetCastBarText : UiAdjustments.SubTweak {

        public class Config {
            public int Offset = 8;
        }

        public override string Name => "Reposition Target Castbar Text";
        
        private readonly Vector2 buttonSize = new Vector2(26, 22);
        public override void DrawConfig(ref bool changed) {
            if (Enabled) {
                if (ImGui.TreeNode($"{Name}###{GetType().Name}SettingsTreeNode")) {
                    var bSize = buttonSize * ImGui.GetIO().FontGlobalScale;
                    ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
                    if (ImGui.InputInt($"###{GetType().Name}_Offset", ref PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset)) {
                        if (PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset > MaxOffset) PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = MaxOffset;
                        if (PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset < MinOffset) PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = MinOffset;
                        changed = true;
                    }
                    ImGui.SameLine();
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2));
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowUp}", bSize)) {
                        PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 8;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Above progress bar");

                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}", bSize)) {
                        PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 24;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Original Position");

                    
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowDown}", bSize)) {
                        PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset = 32;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Below progress bar");
                    ImGui.PopStyleVar();
                    ImGui.SameLine();
                    ImGui.Text("Ability name vertical offset");
                    ImGui.TreePop();
                }
            } else {
                base.DrawConfig(ref changed);
            }
        }

        public void OnFrameworkUpdate(Framework framework) {
            try {
                HandleBars(framework);
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }

        private void HandleBars(Framework framework, bool reset = false) {

            var focusTargetInfo = framework.Gui.GetAddonByName("_FocusTargetInfo", 1);
            if (focusTargetInfo != null && (focusTargetInfo.Visible || reset)) {
                HandleFocusTargetInfo(focusTargetInfo, reset);
            }


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
            var addonStruct = (AtkUnitBase*) (addon.Address);
            if (addonStruct->RootNode == null) return;
            var rootNode = addonStruct->RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            DoShift(child, reset);
        }

        private unsafe void HandleMainTargetInfo(Addon addon, bool reset = false) {
            var addonStruct =(AtkUnitBase*) (addon.Address);
            if (addonStruct->RootNode == null) return;


            var rootNode = addonStruct->RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            for (var i = 0; i < 8; i++) {
                if (child->PrevSiblingNode == null) return;
                child = child->PrevSiblingNode;
            }

            DoShift(child, reset);
        }

        private unsafe void HandleFocusTargetInfo(Addon addon, bool reset = false) {
            var addonStruct = (AtkUnitBase*)(addon.Address);
            if (addonStruct->RootNode == null) return;


            var rootNode = addonStruct->RootNode;
            if (rootNode->ChildNode == null) return;
            var child = rootNode->ChildNode;
            for (var i = 0; i < 6; i++) {
                if (child->PrevSiblingNode == null) return;
                child = child->PrevSiblingNode;
            }

            DoShift(child, reset);
        }

        private const int MinOffset = 0;
        private const int MaxOffset = 48;

        private unsafe void DoShift(AtkResNode* node, bool reset = false) {
            if (node == null) return;
            if (node->ChildCount != 5) return; // Should have 5 children
            var skillTextNode = UiAdjustments.GetResNodeByPath(node, Child, Previous, Previous, Previous);
            if (skillTextNode == null) return;
            var p = PluginConfig.UiAdjustments.ShiftTargetCastBarText.Offset;
            if (p < MinOffset) p = MinOffset;
            if (p > MaxOffset) p = MaxOffset;
            Marshal.WriteInt16(new IntPtr(skillTextNode), 0x92, reset ? (short) 24 : (short) p);
        }

        public override void Enable() {
            if (Enabled) return;
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            Enabled = true;
        }

        public override void Disable() {
            if (!Enabled) return;
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            SimpleLog.Debug($"[{GetType().Name}] Reset");
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
