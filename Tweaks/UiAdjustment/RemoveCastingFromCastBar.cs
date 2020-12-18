using System;
using System.Numerics;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;


namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public RemoveCastingFromCastBar.Config RemoveCastingFromCastBar = new RemoveCastingFromCastBar.Config();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class RemoveCastingFromCastBar : UiAdjustments.SubTweak {

        public class Config {
            public int TimerOffset = 82;
        }

        public override string Name => "Remove 'Casting' from Cast Bar";

        private const int MinOffset = 0;
        private const int MaxOffset = 82;

        private Vector2 buttonSize = new Vector2(26, 22);

        public override void DrawConfig(ref bool changed) {
           
            if (Enabled) {
                if (ImGui.TreeNode($"{Name}###{GetType().Name}SettingsTreeNode")) {
                    ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
                    if (ImGui.InputInt($"###{GetType().Name}_TimerOffset", ref PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset)) {
                        if (PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset > MaxOffset) PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset = MaxOffset;
                        if (PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset < MinOffset) PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset = MinOffset;
                        changed = true;
                    }
                    ImGui.SameLine();

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2));
                    var bSize = buttonSize * ImGui.GetIO().FontGlobalScale;
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char)FontAwesomeIcon.AlignLeft}", bSize)) {
                        PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset = 0;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Left Aligned");
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char)FontAwesomeIcon.AlignCenter}", bSize)) {
                        PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset = 33;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Center Aligned");

                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{(char)FontAwesomeIcon.AlignRight}", bSize)) {
                        PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset = 82;
                        changed = true;
                    }
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Right Aligned (Original)");

                    ImGui.PopStyleVar();
                    

                    ImGui.SameLine();
                    ImGui.Text("Adjust countdown position");
                    ImGui.TreePop();
                }
                

            } else {
                base.DrawConfig(ref changed);
            }
        }



        public override void Setup() {
            Ready = true;
        }

        public void OnFrameworkUpdate(Framework framework) {
            try {
                UpdateText(framework);
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }

        private float rot = 0;
        private unsafe void UpdateText(Framework framework, bool reset = false) {
            var castBar = framework.Gui.GetUiObjectByName("_CastBar", 1);
            if (castBar == IntPtr.Zero) return;
            var addonStruct = (AtkUnitBase*) (castBar);

            var castingResNode = UiAdjustments.GetResNodeByPath(addonStruct->RootNode, Child, Previous, Child, Previous, Previous, Previous, Child);
            if (castingResNode == null) return;
            var castingText = (AtkTextNode*) castingResNode;
            castingText->FontSize = reset ? (byte) 12 : (byte) 0;

            var countdownResNode = UiAdjustments.GetResNodeByPath(addonStruct->RootNode, Child, Previous, Child, Previous, Previous);
            if (countdownResNode != null) {
                var o = PluginConfig.UiAdjustments.RemoveCastingFromCastBar.TimerOffset;
                if (o < MinOffset) o = MinOffset;
                if (o > MaxOffset) o = MaxOffset;
                countdownResNode->Width = reset ? (ushort) 82 : (ushort) o;
            }
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
            UpdateText(PluginInterface.Framework, true);
            Enabled = false;
        }

        public override void Dispose() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            Enabled = false;
            Ready = false;
        }
    }
}
