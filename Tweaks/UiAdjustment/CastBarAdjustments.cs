using System;
using System.Numerics;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public CastBarAdjustments.Configs CastBarAdjustments = new CastBarAdjustments.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class CastBarAdjustments : UiAdjustments.SubTweak {
        public override string Name => "Cast Bar Adjustments";
        public override string Description => "Allows hiding or moving specific parts of the castbar.";
        public enum Alignment : byte {
            Left = 3,
            Center = 4,
            Right = 5,
        }

        public class Configs {
            public bool RemoveCastingText;
            public bool RemoveIcon;
            public bool RemoveCounter;
            public bool RemoveName;

            public bool SlideCast;

            public Alignment AlignName = Alignment.Left;
            public Alignment AlignCounter = Alignment.Right;

            public int OffsetNamePosition = 0;
            public int OffsetCounterPosition = 0;

        }

        public Configs Config => PluginConfig.UiAdjustments.CastBarAdjustments;

        private bool DrawAlignmentSelector(string name, ref Alignment selectedAlignment) {
            var changed = false;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);

            ImGui.PushID(name);
            ImGui.BeginGroup();
            ImGui.PushFont(UiBuilder.IconFont);

            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == Alignment.Left ? 0xFF00A5FF: 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignLeft}##{name}")) {
                selectedAlignment = Alignment.Left;
                changed = true;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == Alignment.Center ? 0xFF00A5FF : 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignCenter}##{name}")) {
                selectedAlignment = Alignment.Center;
                changed = true;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == Alignment.Right ? 0xFF00A5FF : 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignRight}##{name}")) {
                selectedAlignment = Alignment.Right;
                changed = true;
            }
            ImGui.PopStyleColor();

            ImGui.PopFont();
            ImGui.PopStyleVar();
            ImGui.SameLine();
            ImGui.Text(name);
            ImGui.EndGroup();

            ImGui.PopStyleVar();
            ImGui.PopID();
            return changed;
        }
        
        private float configAlignmentX;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Hide 'Casting' Text", ref Config.RemoveCastingText);
            hasChanged |= ImGui.Checkbox("Hide Icon", ref Config.RemoveIcon);
            hasChanged |= ImGui.Checkbox("Hide Countdown Text", ref Config.RemoveCounter);
            if (Config.RemoveCastingText && !Config.RemoveCounter) {
                ImGui.SameLine();
                if (ImGui.GetCursorPosX() > configAlignmentX) configAlignmentX = ImGui.GetCursorPosX();
                ImGui.SetCursorPosX(configAlignmentX);
                hasChanged |= DrawAlignmentSelector("Align Countdown Text", ref Config.AlignCounter);

                ImGui.SetCursorPosX(configAlignmentX);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset##offsetCounterPosition", ref Config.OffsetCounterPosition);
                if (Config.OffsetCounterPosition < -100) Config.OffsetCounterPosition = -100;
                if (Config.OffsetCounterPosition > 100) Config.OffsetCounterPosition = 100;

            }
            hasChanged |= ImGui.Checkbox("Hide Ability Name", ref Config.RemoveName);
            if (!Config.RemoveName) {
                ImGui.SameLine();
                if (ImGui.GetCursorPosX() > configAlignmentX) configAlignmentX = ImGui.GetCursorPosX();
                ImGui.SetCursorPosX(configAlignmentX);
                hasChanged |= DrawAlignmentSelector("Align Ability Name", ref Config.AlignName);
                ImGui.SetCursorPosX(configAlignmentX);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset##offsetNamePosition", ref Config.OffsetNamePosition);

                if (Config.OffsetNamePosition < -100) Config.OffsetNamePosition = -100;
                if (Config.OffsetNamePosition > 100) Config.OffsetNamePosition = 100;
            }

            hasChanged |= ImGui.Checkbox("Show SlideCast Marker", ref Config.SlideCast);

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);


            if (hasChanged) {
                UpdateCastBar(true);
            }
        };

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            UpdateCastBar(true);
            base.Disable();
        }

        private void FrameworkOnUpdate(Framework framework) {
            try {
                UpdateCastBar();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
        
        public void UpdateCastBar(bool reset = false) {
            var castBar = Common.GetUnitBase("_CastBar");
            if (castBar == null) return;
            if (castBar->UldManager.NodeList == null || castBar->UldManager.NodeListCount < 12) return;

            var barNode = castBar->UldManager.NodeList[3];
            
            var icon = (AtkComponentNode*) castBar->UldManager.NodeList[7];
            var countdownText = (AtkTextNode*) castBar->UldManager.NodeList[8];
            var castingText = (AtkTextNode*) castBar->UldManager.NodeList[9];
            var skillNameText = (AtkTextNode*) castBar->UldManager.NodeList[11];

            if (reset) {
                UiHelper.Show(icon);
                UiHelper.Show(countdownText);
                UiHelper.Show(castingText);
                UiHelper.Show(skillNameText);

                UiHelper.SetSize(skillNameText, 170, null);
                UiHelper.SetPosition(skillNameText, barNode->X + 4, null);

                UiHelper.SetSize(countdownText, 42, null);
                UiHelper.SetPosition(countdownText, 170, null);

                countdownText->AlignmentFontType = 0x25;
                skillNameText->AlignmentFontType = 0x03;
                
                return;
            }

            if (Config.RemoveIcon) UiHelper.Hide(icon);
            if (Config.RemoveName) UiHelper.Hide(skillNameText);
            if (Config.RemoveCounter) UiHelper.Hide(countdownText);
            if (Config.RemoveCastingText) UiHelper.Hide(castingText);

            if (Config.RemoveCastingText && !Config.RemoveCounter) {
                countdownText->AlignmentFontType = (byte) (0x20 | (byte) Config.AlignCounter);
                UiHelper.SetSize(countdownText, barNode->Width - 8, null);
                UiHelper.SetPosition(countdownText, (barNode->X + 4) + Config.OffsetCounterPosition, null);
            } else {
                countdownText->AlignmentFontType = (byte)(0x20 | (byte)Alignment.Right);
                UiHelper.SetSize(countdownText, 42, null);
                UiHelper.SetPosition(countdownText, 170, null);
            }

            if (!Config.RemoveName) {
                skillNameText->AlignmentFontType = (byte) (0x00 | (byte) Config.AlignName);
                UiHelper.SetPosition(skillNameText, (barNode->X + 4) + Config.OffsetNamePosition, null);
                UiHelper.SetSize(skillNameText, barNode->Width - 8, null);
            }
        }
    }
}
