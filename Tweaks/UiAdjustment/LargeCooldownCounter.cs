using Dalamud.Game.Internal;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public LargeCooldownCounter.Configs LargeCooldownCounter = new LargeCooldownCounter.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class LargeCooldownCounter : UiAdjustments.SubTweak {

        public class Configs {
            public bool FastUpdates;
            public bool DoCross;
            public bool DoWCross;
        }

        private Configs Config => PluginConfig.UiAdjustments.LargeCooldownCounter;

        public override void DrawConfig(ref bool hasChanged) {
            if (Enabled) {
                if (ImGui.TreeNode($"{Name}")) {
                    hasChanged |= ImGui.Checkbox("Fast Updates##largeCooldownCounterFastUpdates", ref Config.FastUpdates);
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Enabled: Update all hotbars every tick.\nDisabled: Update one hotbar per tick.");
                    }

                    hasChanged |= ImGui.Checkbox("Update Cross Hotbar##largeCooldownCounterDoCross", ref Config.DoCross);
                    hasChanged |= ImGui.Checkbox("Update W Cross Hotbar##largeCooldownCounterDoWCross", ref Config.DoWCross);

                    if (hasChanged) {
                        UpdateAll(true);
                        UpdateAll();
                    }
                    ImGui.TreePop();
                }
            } else {
                base.DrawConfig(ref hasChanged);
            }
        }

        public override string Name => "Large Cooldown Counter";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }

        private int c;

        private readonly string[] actionBarNames = new string[] {
            "_ActionBar",
            "_ActionBar01",
            "_ActionBar02",
            "_ActionBar03",
            "_ActionBar04",
            "_ActionBar05",
            "_ActionBar06",
            "_ActionBar07",
            "_ActionBar08",
            "_ActionBar09",
        };

        private void FrameworkUpdate(Framework framework) {
            if (Config.FastUpdates) {
                UpdateAll();
            } else {
                unchecked {
                    if (c++ >= actionBarNames.Length) c = 0;
                    UpdateIndex(c);
                }
                if (Config.DoCross) UpdateCross();
                if (Config.DoWCross) UpdateWCross();
            }
        }

        private void UpdateAll(bool reset = false) {
            for (var i = 0; i < actionBarNames.Length; i++) UpdateIndex(i, reset);
            if (Config.DoCross || reset) UpdateCross(reset);
            if (Config.DoWCross || reset) UpdateWCross(reset);
        }

        private void UpdateCross(bool reset = false) {
            var actionCross = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_ActionCross", 1);
            if (actionCross == null) return;

            for (var i = 8; i < 12 && i < actionCross->ULDData.NodeListCount; i++) {
                var component = (AtkComponentNode*) actionCross->ULDData.NodeList[i];
                if (component == null) continue;
                if (component->Component->ULDData.NodeListCount != 4) continue;

                for (var j = 0; j < component->Component->ULDData.NodeListCount; j++) {
                    var dragDropComponent = (AtkComponentNode*) component->Component->ULDData.NodeList[j];
                    UpdateIcon(dragDropComponent, reset);
                }
            }
        }

        private void UpdateWCross(bool reset = false) {
            if (reset || Config.FastUpdates || c % 2 == 0) {
                var left = (AtkUnitBase*) PluginInterface.Framework.Gui.GetUiObjectByName("_ActionDoubleCrossL", 1);
                UpdateWCross(left, reset);
            }
            if (reset || Config.FastUpdates || c % 2 == 1) {
                var right = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("_ActionDoubleCrossR", 1);
                UpdateWCross(right, reset);
            }
        }

        private void UpdateWCross(AtkUnitBase* unitBase, bool reset = false) {
            if (unitBase == null) return;
            for (var i = 5; i < 7 && i < unitBase->ULDData.NodeListCount; i++) {
                var component = (AtkComponentNode*)unitBase->ULDData.NodeList[i];
                if (component == null) continue;
                if (component->Component->ULDData.NodeListCount != 4) continue;

                for (var j = 0; j < component->Component->ULDData.NodeListCount; j++) {
                    var dragDropComponent = (AtkComponentNode*)component->Component->ULDData.NodeList[j];
                    UpdateIcon(dragDropComponent, reset);
                }
            }
        }

        private void UpdateIndex(int i, bool reset = false) {
            if (i < 0 || i >= actionBarNames.Length) return;
            var actionBarName = actionBarNames[i];
            var actionBar = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName(actionBarName, 1);
            if (actionBar == null) return;
            UpdateHotbar(actionBar, reset);
        }

        private void UpdateHotbar(AtkUnitBase* hotbar, bool reset = false) {
            if (hotbar == null) return;
            if (hotbar->ULDData.NodeListCount < 22) return;
            for (var i = 0; i < 12; i++) {
                UpdateActionIcon((AtkComponentNode*) hotbar->ULDData.NodeList[20 - i], reset);
            }
        }

        private void UpdateActionIcon(AtkComponentNode* baseComponent, bool reset = false) {
            if (baseComponent == null) return;
            if (baseComponent->Component->ULDData.NodeListCount < 1) return;
            var dragDropComponent = (AtkComponentNode*)baseComponent->Component->ULDData.NodeList[0];
            UpdateIcon(dragDropComponent, reset);
        }

        private void UpdateIcon(AtkComponentNode* dragDropComponent, bool reset = false) {
            if (dragDropComponent == null) return;
            if ((dragDropComponent->AtkResNode.Flags & 0x10) != 0x10) return;
            if (dragDropComponent->Component->ULDData.NodeListCount < 3) return;
            var iconComponent = (AtkComponentNode*) dragDropComponent->Component->ULDData.NodeList[2];
            if (iconComponent == null) return;
            var cooldownTextNode = (AtkTextNode*) iconComponent->Component->ULDData.NodeList[13];
            if (cooldownTextNode->AtkResNode.Type != NodeType.Text) return;
            if ((cooldownTextNode->AtkResNode.Flags & 0x10) != 0x10) return;
            if (cooldownTextNode == null) return;
            if (cooldownTextNode->EdgeColor.R != 0x33) reset = true;
            cooldownTextNode->AtkResNode.X = reset ? 3 : 0;
            cooldownTextNode->AtkResNode.Y = reset ? 37 : 0;
            cooldownTextNode->AtkResNode.Width = (ushort) (reset ? 48 : 46);
            cooldownTextNode->AtkResNode.Height = (ushort) (reset ? 12 : 46);

            cooldownTextNode->AlignmentFontType = (byte) (reset ? AlignmentType.Left : AlignmentType.Center);
            cooldownTextNode->FontSize = (byte) (reset ? 12 : 18);
            
            cooldownTextNode->AtkResNode.Flags_2 |= 0x1;
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            UpdateAll(true);
            base.Disable();
        }
    }
}
