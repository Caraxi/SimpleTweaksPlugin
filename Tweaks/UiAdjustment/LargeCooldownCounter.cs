using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class LargeCooldownCounter : UiAdjustments.SubTweak {
        
        public override string Name => "Large Cooldown Counter";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }

        private readonly string[] allActionBars = {
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
            "_ActionCross",
            "_ActionDoubleCrossL",
            "_ActionDoubleCrossR",
        };


        private void FrameworkUpdate(Framework framework) {
            UpdateAll();
        }

        private void UpdateAll(bool reset = false) {
            foreach (var actionBar in allActionBars) {
                var ab = (AddonActionBarBase*) PluginInterface.Framework.Gui.GetUiObjectByName(actionBar, 1);
                if (ab == null || ab->ActionBarSlotsAction == null) continue;
                for (var i = 0; i < ab->HotbarSlotCount; i++) {
                    var slot = ab->ActionBarSlotsAction[i];
                    if ((slot.PopUpHelpTextPtr != null || reset) && slot.Icon != null) {
                        UpdateIcon(slot.Icon, reset);
                    }
                }
            }
        }
        
        private void UpdateIcon(AtkComponentNode* iconComponent, bool reset = false) {
            if (iconComponent == null) return;
            var cooldownTextNode = (AtkTextNode*)iconComponent->Component->ULDData.NodeList[13];
            if (cooldownTextNode->AtkResNode.Type != NodeType.Text) return;
            if ((cooldownTextNode->AtkResNode.Flags & 0x10) != 0x10) return;
            if (cooldownTextNode == null) return;
            if (cooldownTextNode->EdgeColor.R != 0x33) reset = true;
            cooldownTextNode->AtkResNode.X = reset ? 3 : 0;
            cooldownTextNode->AtkResNode.Y = reset ? 37 : 0;
            cooldownTextNode->AtkResNode.Width = (ushort)(reset ? 48 : 46);
            cooldownTextNode->AtkResNode.Height = (ushort)(reset ? 12 : 46);
            cooldownTextNode->AlignmentFontType = (byte)(reset ? AlignmentType.Left : AlignmentType.Center);
            cooldownTextNode->FontSize = (byte)(reset ? 12 : 18);
            cooldownTextNode->AtkResNode.Flags_2 |= 0x1;
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            UpdateAll(true);
            base.Disable();
        }
    }
}
