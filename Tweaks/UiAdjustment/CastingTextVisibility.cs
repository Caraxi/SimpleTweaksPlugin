using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static Lumina.Data.Parsing.Uld.NodeData;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    internal unsafe class CastingTextVisibility : UiAdjustments.SubTweak
    {

        public class Configs : TweakConfig
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

        public Configs Config { get; private set; }
        private AtkImageNode* focusTargetImageNode = null;
        private AtkImageNode* targetImageNode = null;

        private int focusTargetDefaultNodeCount = 19;
        private int targetDefaultNodeCount = 54;

        private int focusTargetTextNodeIndex = 16;
        private int targetTextNodeIndex = 44;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
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
            CreateImageNodes();
            Common.FrameworkUpdate += FrameworkUpdate;
            base.Enable();
        }

        protected override void Disable()
        {
            SaveConfig(Config);
            DeleteImageNodes();
            ResetTextNodes();
            Common.FrameworkUpdate -= FrameworkUpdate;
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

        private void Update()
        {
            TryDrawUi("_FocusTargetInfo", 1, focusTargetTextNodeIndex, focusTargetDefaultNodeCount,
                Config.UseCustomFocusColor, focusTargetImageNode, Config.FocusTextColor,
                Config.FocusEdgeColor, Config.FocusFontSize, DrawFocusTargetBackground);

            TryDrawUi("_TargetInfo", 1, targetTextNodeIndex, targetDefaultNodeCount,
               Config.UseCustomTargetColor, targetImageNode, Config.TargetTextColor,
               Config.TargetEdgeColor, Config.TargetFontSize, DrawTargetBackground);
        }

        unsafe delegate void DrawBackgroundAction(AtkTextNode* node);

        private void TryDrawUi(string unitBaseName, int unitBaseId, int nodeIndex, int defaultNodeCount, bool useCustomColor, AtkImageNode* imageNode,
            Vector4 textColor, Vector4 edgeColor, int fontSize, DrawBackgroundAction drawBackground)
        {
            var ui = Common.GetUnitBase(unitBaseName, unitBaseId);
            if (NodeExists(ui, nodeIndex))
            {
                var textNode = (AtkTextNode*)ui->UldManager.NodeList[nodeIndex];

                if (ui->IsVisible && useCustomColor)
                {
                    TryLinkNode(ui, defaultNodeCount, imageNode);
                    imageNode->AtkResNode.ToggleVisibility(textNode->AtkResNode.IsVisible);                    
                    AdjustTextColorsAndFontSize(textNode, textColor, edgeColor, fontSize);
                    drawBackground(textNode);
                }
                else
                {
                    if (imageNode != null)
                        imageNode->AtkResNode.ToggleVisibility(false);
                    ResetText(textNode);
                }
            }
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
            var ftext = (AtkTextNode*)fui->UldManager.NodeList[focusTargetTextNodeIndex];
            var ttext = (AtkTextNode*)tui->UldManager.NodeList[targetTextNodeIndex];
            if (fui != null)  ResetText(ftext);
            if (tui != null)  ResetText(ttext);
        }

        private void TryLinkNode(AtkUnitBase* ui, int defaultNodeCount, AtkImageNode* imageNode)
        {
            if (ui == null || ui->RootNode == null || ui->RootNode->ChildNode == null) return;         
            if (imageNode == null) return;
            if (ui->UldManager.NodeListCount <= defaultNodeCount)
                UiHelper.LinkNodeAtEnd(&imageNode->AtkResNode, ui);            
            else
                imageNode = (AtkImageNode*)ui->UldManager.NodeList[focusTargetDefaultNodeCount];            
        }

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

        private void DrawFocusTargetBackground(AtkTextNode* textNode)
        {
            //https://accessibility.psu.edu/legibility/fontsize/ idk
            var textNodeHeight = textNode->AtkResNode.Height;
            var imageNodeHeight = (int)(1.33 * Config.FocusFontSize + 1) + Config.FocusBackgroundHeight;
            var imageNodeWidth = Config.FocusAutoAdjustWidth ? GetWidthFromTextLength(textNode) : Config.FocusBackgroundWidth;
            
            //text node height is affected by the 'Reposition Target Castbar Text' tweak.            
            var xOffset = 0;
            var yOffset = ((24) - textNodeHeight + (imageNodeHeight - 24)) - Config.FocusBackgroundHeight / 2;

            switch (textNode->AlignmentType)
            {
                case AlignmentType.BottomLeft:
                    xOffset = 6;
                    break;
                case AlignmentType.BottomRight:
                    xOffset = 6 - (imageNodeWidth - 192);
                    break;
                case AlignmentType.Bottom:
                    xOffset = 6 - ((imageNodeWidth - 192) + 1) / 2;
                    break;
            }

            UiHelper.SetPosition(focusTargetImageNode, textNode->AtkResNode.X + xOffset, textNode->AtkResNode.Y - yOffset);
            UiHelper.SetSize(focusTargetImageNode, imageNodeWidth, imageNodeHeight);

            focusTargetImageNode->AtkResNode.Color.A = (byte)(Config.FocusBackgroundColor.W * 255);
            focusTargetImageNode->AtkResNode.AddRed = (byte)(Config.FocusBackgroundColor.X * 255);
            focusTargetImageNode->AtkResNode.AddGreen = (byte)(Config.FocusBackgroundColor.Y * 255);
            focusTargetImageNode->AtkResNode.AddBlue = (byte)(Config.FocusBackgroundColor.Z * 255);
        }
      
        private void DrawTargetBackground(AtkTextNode* textNode)
        {
            var textNodeHeight = textNode->AtkResNode.Height;
            var imageNodeHeight = (int)(1.33 * Config.TargetFontSize + 1) + Config.TargetBackgroundHeight;
            var imageNodeWidth = Config.TargetAutoAdjustWidth ? GetWidthFromTextLength(textNode) : Config.TargetBackgroundWidth;
          
            var xOffset = 0;
            var yOffset = ((14) - textNodeHeight + (imageNodeHeight - 24)) - Config.TargetBackgroundHeight / 2;

            switch (textNode->AlignmentType)
            {
                case AlignmentType.BottomLeft:
                    xOffset = 244;
                    break;
                case AlignmentType.BottomRight:
                    xOffset = 244 - ((imageNodeWidth - 192));
                    break;
                case AlignmentType.Bottom:
                    xOffset = 244 - ((imageNodeWidth - 192) + 1) / 2;
                    break;
            }

            UiHelper.SetPosition(targetImageNode, textNode->AtkResNode.X + xOffset, textNode->AtkResNode.Y - yOffset);
            UiHelper.SetSize(targetImageNode, imageNodeWidth, imageNodeHeight);

            targetImageNode->AtkResNode.Color.A = (byte)(Config.TargetBackgroundColor.W * 255);
            targetImageNode->AtkResNode.AddRed = (byte)(Config.TargetBackgroundColor.X * 255);
            targetImageNode->AtkResNode.AddBlue = (byte)(Config.TargetBackgroundColor.Z * 255);
            targetImageNode->AtkResNode.AddGreen = (byte)(Config.TargetBackgroundColor.Y * 255);
        }

        //font is not monospace, so not 100% accurate
        private int GetWidthFromTextLength(AtkTextNode* textNode)
        {
            var textLength = textNode->NodeText.ToString().Length;

            return (int)(0.7 * textLength * textNode->FontSize + 1);
        }

        private void UnlinkImageNode(AtkUnitBase* ui, int defaultNodeCount, AtkImageNode* imageNode)
        {
            if (ui == null) return;
            if (imageNode == null) return;
            if (ui->UldManager.NodeListCount > defaultNodeCount)
                UiHelper.UnlinkNode(&imageNode->AtkResNode, ui);
        }

        private void TryUnlinkImageNodes()
        {
            var focusTargetUi = Common.GetUnitBase("_FocusTargetInfo", 1);
            UnlinkImageNode(focusTargetUi, focusTargetDefaultNodeCount, focusTargetImageNode);

            var targetUi = Common.GetUnitBase("_TargetInfo", 1);
            UnlinkImageNode(targetUi, targetDefaultNodeCount, targetImageNode);
        }

        private void CreateImageNodes()
        {
            CreateImageNode(out focusTargetImageNode, 0);
            CreateImageNode(out targetImageNode, 1);
        }

        private void CreateImageNode(out AtkImageNode* node, int id)
        {
            node = UiHelper.MakeImageNode(CustomNodes.Get(nameof(CastingTextVisibility), id), new UiHelper.PartInfo(0, 0, 0, 0));
            if (node == null)
            {
                SimpleLog.Error("Casting Text Visibiility: Failed to make background image node");
                return;
            }
            node->AtkResNode.NodeFlags = NodeFlags.Enabled | NodeFlags.AnchorLeft | NodeFlags.Visible;
            node->WrapMode = 1;
            node->Flags = 0;
        }

        private void DeleteImageNodes()
        {
            TryUnlinkImageNodes();

            if (focusTargetImageNode != null)

                if (targetImageNode != null)
                    UiHelper.FreeImageNode(targetImageNode);

            focusTargetImageNode = null;
            targetImageNode = null;
        }

        private bool NodeExists(AtkUnitBase* unitBase, int index)
        {
            return unitBase != null && unitBase->UldManager.NodeListCount > index;
        }
    }
}
