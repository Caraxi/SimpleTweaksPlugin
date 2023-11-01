using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Data.Parsing.Uld.NodeData;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    [TweakName("Casting Text Visibility")]
    [TweakDescription("Change the font size, color, and background of the casting text.")]
    [TweakAuthor("img")]
    [TweakAutoConfig]
    [TweakReleaseVersion("1.0.0.0")]

    internal unsafe class CastingTextVisibility : UiAdjustments.SubTweak
    {
        private uint focusTargetImageNodeid => CustomNodes.Get(this, "CTVFocus");
        private uint targetImageNodeid => CustomNodes.Get(this, "CTVTarget");

        private uint focusTargetTextNodeId = 5;
        private uint targetTextNodeId = 12;

        private Configuration Config { get; set; } = null!;
        public class Configuration : TweakConfig
        {
            public bool UseCustomFocusColor = false;
            public bool UseCustomTargetColor = false;

            public Vector4 FocusTextColor = new Vector4(1);
            public Vector4 FocusEdgeColor = new Vector4(115 / 255f, 85 / 255f, 15 / 255f, 1);
            public Vector4 FocusBackgroundColor = new Vector4(0);
            public int FocusFontSize = 14;
            public int FocusBackgroundWidth = 192;
            public int FocusBackgroundHeight = 0;
            public bool FocusAutoAdjustWidth = false;

            public Vector4 TargetTextColor = new Vector4(1);
            public Vector4 TargetEdgeColor = new Vector4(157 / 255f, 131 / 255f, 91 / 255f, 1);
            public Vector4 TargetBackgroundColor = new Vector4(0);
            public int TargetFontSize = 14;
            public int TargetBackgroundWidth = 192;
            public int TargetBackgroundHeight = 0;
            public bool TargetAutoAdjustWidth = false;
        }

        private void DrawConfig()
        {
            ImGui.Checkbox("Focus Target", ref Config.UseCustomFocusColor);

            if (Config.UseCustomFocusColor)
            {
                ImGui.SetColorEditOptions(ImGuiColorEditFlags.None);
                ImGui.Indent();
                ImGui.ColorEdit4("Text Color##FocusTarget", ref Config.FocusTextColor);
                ImGui.ColorEdit4("Edge Color##FocusTarget", ref Config.FocusEdgeColor);
                ImGui.ColorEdit4("Background Color##FocusTarget", ref Config.FocusBackgroundColor);
                ImGui.Checkbox("Auto Adjust Width##FocusTarget", ref Config.FocusAutoAdjustWidth);
                ImGui.DragInt("Background Width##FocusTarget", ref Config.FocusBackgroundWidth, 0.6f, 90, 800);
                ImGui.DragInt("Background Height Padding##FocusTarget", ref Config.FocusBackgroundHeight, 0.6f, -10, 30);
                ImGui.DragInt("Font Size##FocusTarget", ref Config.FocusFontSize, 0.4f, 12, 50);
                ImGui.Unindent();
                ImGui.NewLine();
            }

            ImGui.Checkbox("Target", ref Config.UseCustomTargetColor);

            if (Config.UseCustomTargetColor)
            {
                ImGui.Indent();
                ImGui.ColorEdit4("Text Color##Target", ref Config.TargetTextColor);
                ImGui.ColorEdit4("Edge Color##Target", ref Config.TargetEdgeColor);
                ImGui.ColorEdit4("Background Color##Target", ref Config.TargetBackgroundColor);
                ImGui.Checkbox("Auto Adjust Width##Target", ref Config.TargetAutoAdjustWidth);
                ImGui.DragInt("Background Width##Target", ref Config.TargetBackgroundWidth, 0.6f, 90, 800);
                ImGui.DragInt("Background Height Padding##Target", ref Config.TargetBackgroundHeight, 0.6f, -10, 30);
                ImGui.DragInt("Font Size##Target", ref Config.TargetFontSize, 0.4f, 12, 50);
                ImGui.Unindent();
                ImGui.NewLine();
            }
        }

        protected override void Disable()
        {
            ResetTextNodes();
            FreeAllNodes();            
        }

        [AddonPostRequestedUpdateAttribute("_TargetInfo", "_FocusTargetInfo")]
        private void OnAddonRequestedUpdate(AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon;
            switch (args.AddonName)
            {
                case "_FocusTargetInfo" when addon->IsVisible:
                    UpdateAddOn(addon, focusTargetTextNodeId, focusTargetImageNodeid,
                        Config.UseCustomFocusColor, Config.FocusTextColor,
                        Config.FocusEdgeColor, Config.FocusFontSize, DrawFocusTargetBackground);
                    break;
                case "_TargetInfo" when addon->IsVisible:
                    UpdateAddOn(addon, targetTextNodeId, targetImageNodeid,
                       Config.UseCustomTargetColor,Config.TargetTextColor,
                       Config.TargetEdgeColor, Config.TargetFontSize, DrawTargetBackground);
                    break;
            }
        }

        unsafe delegate void DrawBackgroundAction(AtkTextNode* textNode, AtkImageNode* imageNode);

        private void UpdateAddOn(AtkUnitBase* ui, uint textNodeId, uint imageNodeId,  
            bool useCustomColor, Vector4 textColor, Vector4 edgeColor, 
            int fontSize, DrawBackgroundAction drawBackground)
        {
            var textNode = Common.GetNodeByID<AtkTextNode>(&ui->UldManager, textNodeId);
            if (textNode == null) return;
            
            //'_TargetInfo' text node can sometimes remain visible when targetting non-bnpcs, but parent node invis.
            if (ui->IsVisible && useCustomColor && textNode->AtkResNode.ParentNode->IsVisible)  
            {
                TryMakeNodes(ui, imageNodeId);
                ToggleImageNodeVisibility(textNode->AtkResNode.IsVisible, &ui->UldManager, imageNodeId);
                AdjustTextColorsAndFontSize(textNode, textColor, edgeColor, fontSize);

                var imageNode = GetImageNode(&ui->UldManager, imageNodeId);
                if (imageNode != null) drawBackground(textNode, imageNode);
            }
            else
            {
                ToggleImageNodeVisibility(false, &ui->UldManager, imageNodeId);
                ResetText(textNode);
            }
        }

        private void ToggleImageNodeVisibility(bool visible, AtkUldManager* uldManager, uint nodeId)
        {
            var imageNode = GetImageNode(uldManager, nodeId);
            if (imageNode == null) return;
            imageNode->AtkResNode.ToggleVisibility(visible);
        }

        private AtkImageNode* GetImageNode(AtkUldManager* uldManager, uint nodeId) => Common.GetNodeByID<AtkImageNode>(uldManager, nodeId);        

        private void AdjustTextColorsAndFontSize(AtkTextNode* textNode, Vector4 textColor, Vector4 edgeColor, int fontSize)
        {
            textNode->TextColor.A = (byte)(textColor.W * 255);
            textNode->TextColor.R = (byte)(textColor.X * 255);
            textNode->TextColor.G = (byte)(textColor.Y * 255);
            textNode->TextColor.B = (byte)(textColor.Z * 255);

            textNode->EdgeColor.A = (byte)(edgeColor.W * 255);
            textNode->EdgeColor.R = (byte)(edgeColor.X * 255);
            textNode->EdgeColor.G = (byte)(edgeColor.Y * 255);
            textNode->EdgeColor.B = (byte)(edgeColor.Z * 255);

            textNode->FontSize = (byte)(fontSize);
        }

        private void ResetText(AtkTextNode* textNode)
        {
            var defaultEdgeColor = new Vector4(115 / 255f, 85 / 255f, 15 / 255f, 1);
            AdjustTextColorsAndFontSize(textNode, Vector4.One, defaultEdgeColor, 14);
        }
        private void ResetTextNodes()
        {
            var fui = Common.GetUnitBase("_FocusTargetInfo", 1);
            var tui = Common.GetUnitBase("_TargetInfo", 1);
            var ftext = Common.GetNodeByID<AtkTextNode>(&fui->UldManager, focusTargetTextNodeId);
            var ttext = Common.GetNodeByID<AtkTextNode>(&tui->UldManager, targetTextNodeId);
            if (fui != null) ResetText(ftext);
            if (tui != null) ResetText(ttext);
        }

        private void DrawFocusTargetBackground(AtkTextNode* textNode, AtkImageNode* imageNode)
        {
            var offsetBaseX = 6;
            var offsetBaseY = 24;

            DrawBackground(imageNode, textNode, offsetBaseX, offsetBaseY, Config.FocusFontSize, Config.FocusBackgroundHeight, 
                Config.FocusBackgroundWidth, Config.FocusAutoAdjustWidth, Config.FocusBackgroundColor);
        }

        private void DrawTargetBackground(AtkTextNode* textNode, AtkImageNode* imageNode)
        {            
            var offsetBaseX = 244;
            var offsetBaseY = 14;

            DrawBackground(imageNode, textNode, offsetBaseX, offsetBaseY, Config.TargetFontSize, Config.TargetBackgroundHeight,
                 Config.TargetBackgroundWidth, Config.TargetAutoAdjustWidth, Config.TargetBackgroundColor);
        }

        private void DrawBackground(AtkImageNode* imageNode, AtkTextNode* textNode, int offsetBaseX, 
            int offsetBaseY, int fontSize, int backgroundHeight, int backgroundWidth, bool autoAdjust, Vector4 color)
        {
            //https://accessibility.psu.edu/legibility/fontsize/ idk
            var textNodeHeight = textNode->AtkResNode.Height;
            var imageNodeHeight = (int)(1.33 * fontSize + 1) + backgroundHeight;
            var imageNodeWidth = autoAdjust ? GetWidthFromTextLength(textNode) : backgroundWidth;

            var xOffset = 0;
            var yOffset = ((offsetBaseY) - textNodeHeight + (imageNodeHeight - 24)) - Config.FocusBackgroundHeight / 2;

            switch (textNode->AlignmentType)
            {
                case AlignmentType.BottomLeft:
                    xOffset = offsetBaseX;
                    break;
                case AlignmentType.BottomRight:
                    xOffset = offsetBaseX - (imageNodeWidth - 192);
                    break;
                case AlignmentType.Bottom:
                    xOffset = offsetBaseX - ((imageNodeWidth - 192) + 1) / 2;
                    break;
            }

            UiHelper.SetPosition(imageNode, textNode->AtkResNode.X + xOffset, textNode->AtkResNode.Y - yOffset);
            UiHelper.SetSize(imageNode, imageNodeWidth, imageNodeHeight);

            imageNode->AtkResNode.Color.A = (byte)(color.W * 255);
            imageNode->AtkResNode.AddRed = (byte)(color.X * 255);
            imageNode->AtkResNode.AddGreen = (byte)(color.Y * 255);
            imageNode->AtkResNode.AddBlue = (byte)(color.Z * 255);
        }

        //font is not monospace, so not 100% accurate
        private int GetWidthFromTextLength(AtkTextNode* textNode)
        {
            var textLength = textNode->NodeText.ToString().Length;
            return (int)(0.7 * textLength * textNode->FontSize + 1);
        }

        private void TryMakeNodes(AtkUnitBase* parent, uint imageNodeId)
        {
            var imageNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, imageNodeId);
            if (imageNode is null) CreateImageNode(parent, imageNodeId);
        }

        private void CreateImageNode(AtkUnitBase* parent, uint id)
        {
            var imageNode = UiHelper.MakeImageNode(id, new UiHelper.PartInfo(0, 0, 0, 0));
            if (imageNode == null)
            {
                SimpleLog.Error("Casting Text Visibiility: Failed to make background image node");
                return;
            }
            imageNode->AtkResNode.NodeFlags = NodeFlags.Enabled | NodeFlags.AnchorLeft | NodeFlags.Visible;
            imageNode->WrapMode = 1;
            imageNode->Flags = 0;

            UiHelper.LinkNodeAtEnd((AtkResNode*)imageNode, parent);
        }

        private void FreeAllNodes()
        {            
            var addonTargetInfo = Common.GetUnitBase("_TargetInfo");
            var addonFocusTargetInfo = Common.GetUnitBase("_FocusTargetInfo");
            
            TryFreeImageNode(addonTargetInfo, targetImageNodeid);
            TryFreeImageNode(addonFocusTargetInfo, focusTargetImageNodeid);
        }

        private void TryFreeImageNode(AtkUnitBase* addon, uint nodeId)
        {
            if (addon == null) return;

            var imageNode = Common.GetNodeByID<AtkTextNode>(&addon->UldManager, nodeId);
            if (imageNode is not null)
            {
                UiHelper.UnlinkAndFreeTextNode(imageNode, addon);
            }
        }
    }
}
