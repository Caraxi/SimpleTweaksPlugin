using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Data.Parsing.Uld.NodeData;
using static SimpleTweaksPlugin.Tweaks.UiAdjustment.TargetHP;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public unsafe class CastingTextVisibility : UiAdjustments.SubTweak
    {
        private int _focusTargetTextNodeIndex = 16;
        private int _targetTextNodeIndex = 44;

        public class Configs : TweakConfig
        {
            public bool UseCustomFocusColor = false;
            public bool UseCustomTargetColor = false;

            public Vector4 CustomFocusTextColor = new Vector4(1);
            public Vector4 CustomFocusEdgeColor = new Vector4(115 / 255f, 85 / 255f, 15 / 255f, 1);
            public int CustomFocusFontSize = 14;

            public Vector4 CustomTargetTextColor = new Vector4(1);
            public Vector4 CustomTargetEdgeColor = new Vector4(157 / 255f, 131 / 255f, 91 / 255f, 1);
            public int CustomTargetFontSize = 14;
        }

        public Configs Config { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
        {
            ImGui.Checkbox("Focus Target", ref Config.UseCustomFocusColor);

            if (Config.UseCustomFocusColor)
            {
                ImGui.SetColorEditOptions(ImGuiColorEditFlags.None);
                ImGui.Indent();
                ImGui.ColorEdit4("Text Color##FocusTarget", ref Config.CustomFocusTextColor);
                ImGui.ColorEdit4("Edge Color##FocusTarget", ref Config.CustomFocusEdgeColor);
                ImGui.DragInt("Font Size##FocusTarget", ref Config.CustomFocusFontSize, 0.4f, 12, 50);
                ImGui.Unindent();
                ImGui.NewLine();
            }

            ImGui.Checkbox("Target", ref Config.UseCustomTargetColor);

            if (Config.UseCustomTargetColor)
            {
                ImGui.Indent();
                ImGui.ColorEdit4("Text Color##Target", ref Config.CustomTargetTextColor);
                ImGui.ColorEdit4("Edge Color##Target", ref Config.CustomTargetEdgeColor);
                ImGui.DragInt("Font Size##Target", ref Config.CustomTargetFontSize, 0.4f, 12, 50);
                ImGui.Unindent();
                ImGui.NewLine();
            }
        };

        public override string Name => "Casting Text Visibility";
        public override string Description => "Change casting text color and font size.";

        public override void Setup()
        {
            base.Setup();
        }

        protected override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Common.FrameworkUpdate += FrameworkUpdate;
            base.Enable();
        }

        protected override void Disable()
        {
            SaveConfig(Config);
            Common.FrameworkUpdate -= FrameworkUpdate;
            Update(true);
            base.Disable();
        }

        private void FrameworkUpdate()
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                SimpleLog.Error(ex);
            }
        }

        private void Update(bool reset = false)
        {
            if (reset)
            {
                ResetAll();
                return;
            }

            //focus target
            var focusTargetUi = Common.GetUnitBase("_FocusTargetInfo", 1);
            if (NodeExists(focusTargetUi, _focusTargetTextNodeIndex) && focusTargetUi->IsVisible)
            {
                var textNode = (AtkTextNode*)focusTargetUi->UldManager.NodeList[_focusTargetTextNodeIndex];
                if (Config.UseCustomFocusColor)
                {
                    AdjustTextColorsAndFontSize(textNode, Config.CustomFocusTextColor, Config.CustomFocusEdgeColor, Config.CustomFocusFontSize);
                }
                else
                {
                    ResetFocusTargetText(textNode);
                }
            }

            //target
            var targetUi = Common.GetUnitBase("_TargetInfo", 1);
            if (NodeExists(targetUi, _targetTextNodeIndex) && targetUi->IsVisible)
            {
                var textNode = (AtkTextNode*)targetUi->UldManager.NodeList[_targetTextNodeIndex];
                if (Config.UseCustomTargetColor)
                {                    
                    AdjustTextColorsAndFontSize(textNode, Config.CustomTargetTextColor, Config.CustomTargetEdgeColor, Config.CustomTargetFontSize);
                }
                else
                {
                    ResetTargetText(textNode);
                }
            }
        }

        private void ResetFocusTargetText(AtkTextNode* textNode)
        {
            var defaultFocusEdgeColor = new Vector4(115 / 255f, 85 / 255f, 15 / 255f, 1);
            ResetText(textNode, defaultFocusEdgeColor);
        }

        private void ResetTargetText(AtkTextNode* textNode)
        {
            var defaultTargetEdgeColor = new Vector4(157 / 255f, 131 / 255f, 91 / 255f, 1);
            ResetText(textNode, defaultTargetEdgeColor);
        }

        private void ResetText(AtkTextNode* textNode, Vector4 edgeColor) {
            var defaultTextColor = new Vector4(1);
            var defaultFontSize = 14;
            AdjustTextColorsAndFontSize(textNode, defaultTextColor, edgeColor, defaultFontSize);

        }
        
        private void ResetAll()
        {
            var focusTargetUi = Common.GetUnitBase("_FocusTargetInfo", 1);
            if (NodeExists(focusTargetUi, _focusTargetTextNodeIndex))
            {
                var textNode = (AtkTextNode*)focusTargetUi->UldManager.NodeList[_focusTargetTextNodeIndex];
                ResetFocusTargetText(textNode);
            }

            var targetUi = Common.GetUnitBase("_TargetInfo", 1);
            if (NodeExists(targetUi, _targetTextNodeIndex))
            {
                var textNode = (AtkTextNode*)targetUi->UldManager.NodeList[_targetTextNodeIndex];
                ResetTargetText(textNode);
            }
        }        

        private void AdjustTextColorsAndFontSize(AtkTextNode* textNode, Vector4 textColor, Vector4 edgeColor, int fontSize)
        {
            textNode->TextColor.B = (byte)(textColor.Z * 255);
            textNode->TextColor.A = (byte)(textColor.W * 255);
            textNode->TextColor.R = (byte)(textColor.X * 255);
            textNode->TextColor.G = (byte)(textColor.Y * 255);

            textNode->EdgeColor.G = (byte)(edgeColor.Y * 255);
            textNode->EdgeColor.A = (byte)(edgeColor.W * 255);
            textNode->EdgeColor.R = (byte)(edgeColor.X * 255);
            textNode->EdgeColor.B = (byte)(edgeColor.Z * 255);

            textNode->FontSize = (byte)(fontSize);
            textNode->ResizeNodeForCurrentText();
        }

        /// <summary>
        /// Check if the Unit Base and the node index exists
        /// </summary>
        private bool NodeExists(AtkUnitBase* unitBase, int index)
        {
            return unitBase != null && unitBase->UldManager.NodeListCount > index;
        }
    }
}
