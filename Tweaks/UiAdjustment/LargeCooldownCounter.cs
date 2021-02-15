using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using System;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public LargeCooldownCounter.Configs LargeCooldownCounter = new();
    }
}

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
        public class Configs {
            public Font Font = Font.Default;
            public int FontSizeAdjust;
        }

        public Configs Config => PluginConfig.UiAdjustments.LargeCooldownCounter;
        
        public enum Font {
            Default,
            FontB,
            FontC,
            FontD,
        }
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.BeginCombo("Font###st_uiAdjustment_largeCooldownCounter_fontSelect", $"{Config.Font}")) {
                foreach (var f in (Font[])Enum.GetValues(typeof(Font))) {
                    if (ImGui.Selectable($"{f}##st_uiAdjustment_largeCooldownCount_fontOption", f == Config.Font)) {
                        Config.Font = f;
                        hasChanged = true;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.SliderInt("Font Size Adjust##st_uiAdjustment_largEcooldownCounter_fontSize", ref Config.FontSizeAdjust, -15, 30);
            
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

        private byte DefaultFontSize => Config.Font switch {
            Font.FontB => 14,
            Font.FontC => 15,
            Font.FontD => 34,
            _ => 18,
        };

        private byte GetFontSize() {
            var s = (Config.FontSizeAdjust * 2) + DefaultFontSize;
            if (s < 4) s = 4;
            if (s > 255) s = 255;
            return (byte) s;
        }
        
        private void UpdateIcon(AtkComponentNode* iconComponent, bool reset = false) {
            if (iconComponent == null) return;
            var cooldownTextNode = (AtkTextNode*)iconComponent->Component->ULDData.NodeList[13];
            if (cooldownTextNode->AtkResNode.Type != NodeType.Text) return;
            if (reset == false && (cooldownTextNode->AtkResNode.Flags & 0x10) != 0x10) return;
            if (cooldownTextNode == null) return;
            if (cooldownTextNode->EdgeColor.R != 0x33) reset = true;
            cooldownTextNode->AtkResNode.X = reset ? 3 : 0;
            cooldownTextNode->AtkResNode.Y = reset ? 37 : 0;
            cooldownTextNode->AtkResNode.Width = (ushort)(reset ? 48 : 46);
            cooldownTextNode->AtkResNode.Height = (ushort)(reset ? 12 : 46);
            cooldownTextNode->AlignmentFontType = (byte)(reset ? (byte)AlignmentType.Left : (0x10 * (byte) Config.Font) | (byte) AlignmentType.Center);
            cooldownTextNode->FontSize = (byte)(reset ? 12 : GetFontSize());
            cooldownTextNode->AtkResNode.Flags_2 |= 0x1;
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            UpdateAll(true);
            base.Disable();
        }
    }
}
