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
        }

        private Configs Config => PluginConfig.UiAdjustments.LargeCooldownCounter;

        public override void DrawConfig(ref bool hasChanged) {
            base.DrawConfig(ref hasChanged);
            if (Enabled) {
                ImGui.SameLine();
                hasChanged |= ImGui.Checkbox("Fast Updates##largeCooldownCounterFastUpdates", ref Config.FastUpdates);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Enabled: Update all hotbars every tick.\nDisabled: Update one hotbar per tick.");
                }
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
            }
        }

        private void UpdateAll(bool reset = false) {
            for (var i = 0; i < actionBarNames.Length; i++) UpdateIndex(i, reset);
        }

        private void UpdateIndex(int i, bool reset = false) {
            if (i < 0 || i >= actionBarNames.Length) return;
            var actionBarName = actionBarNames[i];
            var actionBar = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName(actionBarName, 1);
            if (actionBar == null) return;
            UpdateHotbar(actionBar, i, reset);
        }

        private void UpdateHotbar(AtkUnitBase* hotbar, int hotbarIndex, bool reset = false) {
            if (hotbar == null) return;
            if (hotbar->ULDData.NodeListCount < 22) return;
            for (var i = 0; i < 12; i++) {
                UpdateIcon((AtkComponentNode*) hotbar->ULDData.NodeList[20 - i], hotbarIndex, i, reset);
            }
        }

        private void UpdateIcon(AtkComponentNode* baseComponent, int hotbarIndex, int iconIndex, bool reset = false) {
            if (baseComponent == null) return;
            
            if (baseComponent->Component->ULDData.NodeListCount < 1) return;
            var dragDropComponent = (AtkComponentNode*) baseComponent->Component->ULDData.NodeList[0];
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
            // cooldownTextNode->AtkResNode.Width = (ushort) (reset ? 48 : 48);
            cooldownTextNode->AtkResNode.Height = (ushort) (reset ? 12 : 48);

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
