using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Numerics;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Casting Text Visibility")]
[TweakDescription("Change the font size, color, and background of the casting text.")]
[TweakAuthor("img")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.3.0")]
[Changelog("1.9.6.0", "Fixed tweak not working with a split primary target window")]
internal unsafe class CastingTextVisibility : UiAdjustments.SubTweak
{
    private uint focusTargetImageNodeid => CustomNodes.Get(this, "CTVFocus");
    private uint targetImageNodeid => CustomNodes.Get(this, "CTVTarget");

    private uint focusTargetCustomTextNodeid => CustomNodes.Get(this, "CTVFocusText");
    private uint targetCustomTextNodeid => CustomNodes.Get(this, "CTVTargetText");

    private readonly uint focusTargetTextNodeId = 5;
    private readonly uint targetTextNodeId = 12;
    private readonly uint splitTargetTextNodeId = 4;

    private readonly uint focusCastBarId = 8;
    private readonly uint targetCastBarId = 15;


    private Configuration Config { get; set; } = null!;

    public class Configuration : TweakConfig
    {
        public bool UseCustomFocusColor;
        public bool UseCustomTargetColor;

        public Vector4 FocusTextColor = new(1);
        public Vector4 FocusEdgeColor = new(115 / 255f, 85 / 255f, 15 / 255f, 1);
        public Vector4 FocusBackgroundColor = new(0);
        public int FocusFontSize = 14;
        public int FocusBackgroundWidthPadding = 0;
        public int FocusBackgroundHeightPadding = 0;
        public Vector2 FocusPosition = Vector2.One;
        public bool FocusRightAlign = true;

        public Vector4 TargetTextColor = new(1);
        public Vector4 TargetEdgeColor = new(157 / 255f, 131 / 255f, 91 / 255f, 1);
        public Vector4 TargetBackgroundColor = new(0);
        public int TargetFontSize = 14;
        public int TargetBackgroundWidthPadding = 0;
        public int TargetBackgroundHeightPadding = 0;
        public Vector2 TargetPosition = Vector2.One;
        public bool TargetRightAlign = true;
    }

    private void DrawConfig()
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(64 / 255f, 128 / 255f, 100 / 255f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(51 / 255f, 128 / 255f, 95 / 255f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(38 / 255f, 128 / 255f, 89 / 255f, 1));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(23 / 255f, 230 / 255f, 141 / 255f, 1));

        ImGui.Checkbox("Focus Target", ref Config.UseCustomFocusColor);
        if (Config.UseCustomFocusColor)
        {


            ImGui.SetColorEditOptions(ImGuiColorEditFlags.None);
            ImGui.Indent();
            ImGui.ColorEdit4("Text Colour##FocusTarget", ref Config.FocusTextColor);
            ImGui.ColorEdit4("Edge Colour##FocusTarget", ref Config.FocusEdgeColor);
            ImGui.ColorEdit4("Background Colour##FocusTarget", ref Config.FocusBackgroundColor);
            ImGui.DragInt("Width Padding##FocusTarget", ref Config.FocusBackgroundWidthPadding, 0.6f, -10, 100);
            ImGui.DragInt("Height Padding##FocusTarget", ref Config.FocusBackgroundHeightPadding, 0.6f, -10, 30);
            ImGui.DragInt("Font Size##FocusTarget", ref Config.FocusFontSize, 0.4f, 12, 80);

            ImGui.VSliderFloat("##FocusTargetY", new Vector2(20, 160), ref Config.FocusPosition.Y, -80f, 80f, "");
            ImGui.SetNextItemWidth(160);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 6);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 69);
            ImGui.SliderFloat("Position##FocusTargetX", ref Config.FocusPosition.X, -120f, 220f, "", ImGuiSliderFlags.NoInput);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 200);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 20);
            ImGui.Checkbox("Right Align##FocusAlign", ref Config.FocusRightAlign);

            ImGui.Unindent();
            ImGui.NewLine();
        }

        ImGui.Checkbox("Target", ref Config.UseCustomTargetColor);

        if (Config.UseCustomTargetColor)
        {
            ImGui.Indent();
            ImGui.ColorEdit4("Text Colour##Target", ref Config.TargetTextColor);
            ImGui.ColorEdit4("Edge Colour##Target", ref Config.TargetEdgeColor);
            ImGui.ColorEdit4("Background Colour##Target", ref Config.TargetBackgroundColor);
            ImGui.DragInt("Width Padding##Target", ref Config.TargetBackgroundWidthPadding, 0.6f, -10, 100);
            ImGui.DragInt("Height Padding##Target", ref Config.TargetBackgroundHeightPadding, 0.6f, -10, 30);
            ImGui.DragInt("Font Size##Target", ref Config.TargetFontSize, 0.4f, 12, 80);

            ImGui.VSliderFloat("##TargetY", new Vector2(20, 160), ref Config.TargetPosition.Y, -80f, 120f, "");
            ImGui.SetNextItemWidth(160);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 6);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 69);
            ImGui.SliderFloat("Position##TargetX", ref Config.TargetPosition.X, -180f, 460f, "", ImGuiSliderFlags.NoInput);
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 200);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 20);
            ImGui.Checkbox("Right Align##TargetAlign", ref Config.TargetRightAlign);

            ImGui.Unindent();
            ImGui.NewLine();
        }
        ImGui.PopStyleColor(4);
    }

    protected override void Disable()
    {
        ResetTextNodes();
        FreeAllNodes();
    }

    [AddonPostRequestedUpdate("_TargetInfo", "_FocusTargetInfo", "_TargetInfoCastBar")]
    private void OnAddonRequestedUpdate(AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        switch (args.AddonName)
        {
            case "_FocusTargetInfo" when addon->IsVisible:
                UpdateAddOn(addon, focusTargetTextNodeId, focusCastBarId, focusTargetImageNodeid, focusTargetCustomTextNodeid, Config.UseCustomFocusColor,
                    Config.FocusTextColor, Config.FocusEdgeColor, Config.FocusFontSize, Config.FocusPosition, UpdateFocusTargetNodes);
                break;
            case "_TargetInfo" when addon->IsVisible:
                UpdateAddOn(addon, targetTextNodeId, targetCastBarId, targetImageNodeid, targetCustomTextNodeid, Config.UseCustomTargetColor,
                    Config.TargetTextColor, Config.TargetEdgeColor, Config.TargetFontSize, Config.TargetPosition, UpdateTargetNodes);
                break;
            case "_TargetInfoCastBar" when addon->IsVisible:
                UpdateAddOn(addon, splitTargetTextNodeId, targetCastBarId, targetImageNodeid, targetCustomTextNodeid, Config.UseCustomTargetColor,
                    Config.TargetTextColor, Config.TargetEdgeColor, Config.TargetFontSize, Config.TargetPosition, UpdateTargetNodes);
                break;
        }
    }

    delegate void UpdateCustomNodesAction(AtkTextNode* textNode, AtkImageNode* imageNode);

    private void UpdateAddOn(AtkUnitBase* ui, uint textNodeId, uint castBarId, uint imageNodeId, uint customTextNodeId, bool useCustomColor,
        Vector4 textColor, Vector4 edgeColor, int fontSize, Vector2 position, UpdateCustomNodesAction updateCustomNodes)
    {
        var textNode = Common.GetNodeByID<AtkTextNode>(&ui->UldManager, textNodeId);
        var castBarNode = Common.GetNodeByID<AtkImageNode>(&ui->UldManager, castBarId);
        if (textNode == null || castBarNode == null) return;
        //'_TargetInfo' text node can sometimes remain visible when targetting non-bnpcs, but parent node invis.
        if (ui->IsVisible && useCustomColor && textNode->AtkResNode.ParentNode->IsVisible())
        {
            textNode->AtkResNode.ToggleVisibility(false);
            TryMakeNode(ui, imageNodeId, NodeType.Image);
            TryMakeNode(ui, customTextNodeId, NodeType.Text);
            //ToggleNodeVisibility(true, &ui->UldManager, textNodeId);           
            ToggleNodeVisibility(castBarNode->AtkResNode.IsVisible(), &ui->UldManager, imageNodeId);
            ToggleNodeVisibility(castBarNode->AtkResNode.IsVisible(), &ui->UldManager, customTextNodeId);

            var customTextNode = Common.GetNodeByID<AtkTextNode>(&ui->UldManager, customTextNodeId);
            customTextNode->SetText(textNode->GetText());

            AdjustTextColorsAndFontSize(customTextNode, textColor, edgeColor, fontSize);

            var imageNode = Common.GetNodeByID<AtkImageNode>(&ui->UldManager, imageNodeId);
            updateCustomNodes(customTextNode, imageNode);
        }
        else
        {
            ToggleNodeVisibility(false, &ui->UldManager, imageNodeId);
            ToggleNodeVisibility(false, &ui->UldManager, customTextNodeId);
            textNode->AtkResNode.ToggleVisibility(castBarNode->IsVisible());
        }
    }

    private void ToggleNodeVisibility(bool visible, AtkUldManager* uldManager, uint nodeId)
    {
        var node = Common.GetNodeByID<AtkResNode>(uldManager, nodeId);
        if (node == null) return;
        node->ToggleVisibility(visible);
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

        textNode->FontSize = (byte)fontSize;
    }

    private void ResetTextNodes()
    {
        var fui = Common.GetUnitBase("_FocusTargetInfo", 1);
        var tui = Common.GetUnitBase("_TargetInfo", 1);
        var stui = Common.GetUnitBase("_TargetInfoCastBar", 1);
        var ftext = Common.GetNodeByID<AtkTextNode>(&fui->UldManager, focusTargetTextNodeId);
        var ttext = Common.GetNodeByID<AtkTextNode>(&tui->UldManager, targetTextNodeId);
        var sttext = Common.GetNodeByID<AtkTextNode>(&stui->UldManager, splitTargetTextNodeId);
        if (ftext != null) ftext->ToggleVisibility(true);
        if (ttext != null) ttext->ToggleVisibility(true);
        if (sttext != null) sttext->ToggleVisibility(true);
    }

    private void UpdateFocusTargetNodes(AtkTextNode* textNode, AtkImageNode* imageNode) =>
        UpdateCustomNodes(imageNode, textNode, Config.FocusFontSize, Config.FocusBackgroundHeightPadding, Config.FocusBackgroundWidthPadding, Config.FocusBackgroundColor, Config.FocusRightAlign, Config.FocusPosition);

    private void UpdateTargetNodes(AtkTextNode* textNode, AtkImageNode* imageNode) =>
        UpdateCustomNodes(imageNode, textNode, Config.TargetFontSize, Config.TargetBackgroundHeightPadding, Config.TargetBackgroundWidthPadding, Config.TargetBackgroundColor, Config.TargetRightAlign, Config.TargetPosition);

    private void ResizeAndAlignTextNode(AtkTextNode* textNode, bool rightAlign, Vector2 position)
    {
        textNode->TextFlags = (byte)(TextFlags.Edge);
        // anything not TopLeft / Left aligned seems to get a width and height of 0   
        // set to left align to resize node
        textNode->AlignmentType = AlignmentType.TopLeft;
        textNode->ResizeNodeForCurrentText();

        if (rightAlign)
        {
            textNode->AlignmentType = AlignmentType.TopRight;
            textNode->SetPositionFloat(position.X + 160, position.Y * -1);
        }
        else
        {
            textNode->SetPositionFloat(position.X, position.Y * -1); // reverse y to match slider direction
        }
    }
    private void UpdateCustomNodes(AtkImageNode* imageNode, AtkTextNode* textNode, int fontSize, int heightPadding, int widthPadding, Vector4 colour, bool rightAlign, Vector2 position)
    {
        ResizeAndAlignTextNode(textNode, rightAlign, position);
        SetImageNodeSizeAndPosition(imageNode, textNode, fontSize, heightPadding, widthPadding);
        SetImageNodeColour(imageNode, colour);
    }
    private void SetImageNodeSizeAndPosition(AtkImageNode* imageNode, AtkTextNode* textNode, int fontSize, int heightPadding, int widthPadding)
    {
        var paddingRight = (textNode->FontSize / 10) + (textNode->NodeText.Length < 6 ? 4 : 1);

        //https://accessibility.psu.edu/legibility/fontsize/ idk
        var imageNodeHeight = (int)(1.33 * fontSize + 1) + heightPadding;
        var imageNodeWidth = textNode->AtkResNode.Width + paddingRight + widthPadding;
        var imageNodePosX = textNode->AtkResNode.X + (widthPadding / 2);

        if (textNode->AlignmentType == AlignmentType.TopRight)
        {
            textNode->Width = 0; // need to set width to 0 to right align properly
            imageNodePosX = imageNodePosX - imageNodeWidth + paddingRight;
        }

        UiHelper.SetPosition(imageNode, imageNodePosX, textNode->AtkResNode.Y - (heightPadding / 2));


        if (textNode->NodeText.Length == 0) UiHelper.SetSize(imageNode, 0, 0);
        else UiHelper.SetSize(imageNode, imageNodeWidth, imageNodeHeight);
    }

    private void SetImageNodeColour(AtkImageNode* imageNode, Vector4 colour)
    {
        imageNode->AtkResNode.Color.A = (byte)(colour.W * 255);
        imageNode->AtkResNode.AddRed = (byte)(colour.X * 255);
        imageNode->AtkResNode.AddGreen = (byte)(colour.Y * 255);
        imageNode->AtkResNode.AddBlue = (byte)(colour.Z * 255);
    }

    private void TryMakeNode(AtkUnitBase* parent, uint nodeId, NodeType type)
    {
        var node = Common.GetNodeByID<AtkResNode>(&parent->UldManager, nodeId);
        if (node is null) CreateNode(parent, nodeId, type);
    }

    private void CreateNode(AtkUnitBase* parent, uint id, NodeType type)
    {
        var node = type == NodeType.Image ?
            (AtkResNode*)UiHelper.MakeImageNode(id, new UiHelper.PartInfo(0, 0, 0, 0)) :
            (AtkResNode*)UiHelper.MakeTextNode(id);
        if (node == null)
        {
            SimpleLog.Error($"Casting Text Visibiility: Failed to make ${(type == NodeType.Image ? "image" : "text")} node");
            return;
        }

        if (type == NodeType.Image)
        {
            ((AtkImageNode*)node)->WrapMode = 1;
            ((AtkImageNode*)node)->Flags = 0;
        }

        node->NodeFlags = NodeFlags.Enabled | NodeFlags.AnchorLeft | NodeFlags.Visible;
        UiHelper.LinkNodeAtEnd(node, parent);
    }

    private void FreeAllNodes()
    {
        var addonFocusTargetInfo = Common.GetUnitBase("_FocusTargetInfo");
        var addonTargetInfo = Common.GetUnitBase("_TargetInfo");
        var addonTargetInfoCastBar = Common.GetUnitBase("_TargetInfoCastBar");

        TryFreeNode(addonFocusTargetInfo, focusTargetImageNodeid, NodeType.Image);
        TryFreeNode(addonTargetInfo, targetImageNodeid, NodeType.Image);
        TryFreeNode(addonTargetInfoCastBar, targetImageNodeid, NodeType.Image);

        TryFreeNode(addonFocusTargetInfo, focusTargetCustomTextNodeid, NodeType.Text);
        TryFreeNode(addonTargetInfo, targetCustomTextNodeid, NodeType.Text);
        TryFreeNode(addonTargetInfoCastBar, targetCustomTextNodeid, NodeType.Text);
    }

    private void TryFreeNode(AtkUnitBase* addon, uint nodeId, NodeType type)
    {
        if (addon == null) return;

        var node = Common.GetNodeByID<AtkResNode>(&addon->UldManager, nodeId);
        if (node is not null)
        {
            if (type == NodeType.Image)
                UiHelper.UnlinkAndFreeImageNode((AtkImageNode*)node, addon);
            else
                UiHelper.UnlinkAndFreeTextNode((AtkTextNode*)node, addon);
        }
    }
}
