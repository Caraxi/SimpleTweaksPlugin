#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Target Castbar Countdown")]
[TweakDescription("Displays time remaining on targets ability cast.")]
[TweakAuthor("MidoriKami")]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.8.3.1", "Add TopRight option for displaying countdown")]
[Changelog("1.8.9.0", "Add option to disable on primary target")]
[Changelog("1.9.2.1", "Fix updating slowly for really slow castbars")]
[Changelog("1.9.6.0", "Added option to change font size")]
[Changelog("1.9.6.0", "Added option to adjust position")]
[Changelog("1.9.6.0", "Fixed disable on primary and focus target")]
public unsafe class TargetCastbarCountdown : UiAdjustments.SubTweak {
    private uint CastBarTextNodeId => CustomNodes.Get(this, "Countdown");
    private Utf8String* stringBuffer = Utf8String.CreateEmpty();

    private readonly ByteColor textColor = new() { R = 255, G = 255, B = 255, A = 255 };
    private readonly ByteColor edgeColor = new() { R = 142, G = 106, B = 12, A = 255 };
    private readonly ByteColor backgroundColor = new() { R = 0, G = 0, B = 0, A = 0 };

    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig {
        public bool PrimaryTargetEnabled = true;
        public bool FocusTargetEnabled = false;
        public NodePosition FocusTargetPosition = NodePosition.Left;
        public NodePosition CastbarPosition = NodePosition.BottomLeft;
        public int FontSize = 20;
        public int FocusFontSize = 20;
        public Vector2 Offset = Vector2.Zero;
        public Vector2 FocusOffset = Vector2.Zero;
    }

    private enum NodePosition {
        Right,
        Left, 
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private void DrawConfig() {
        var rebuild = ImGui.Checkbox("Enable Primary Target", ref TweakConfig.PrimaryTargetEnabled);
        var hasChanged = false;
        if (TweakConfig.PrimaryTargetEnabled) {
            using (ImRaii.PushIndent()) {
                hasChanged |= DrawCombo(ref TweakConfig.CastbarPosition, "Primary Target");
                ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
                hasChanged |= ImGui.DragFloat2("Position Offset##primary", ref TweakConfig.Offset);
                ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
                hasChanged |= ImGui.SliderInt("Font Size##primary", ref TweakConfig.FontSize, 8, 30);
            }
        }
        
        rebuild |= ImGui.Checkbox("Enable Focus Target", ref TweakConfig.FocusTargetEnabled);
        
        if (TweakConfig.FocusTargetEnabled) {
            using (ImRaii.PushIndent()) {
                hasChanged |= DrawCombo(ref TweakConfig.FocusTargetPosition, "Focus Target");
                ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
                hasChanged |= ImGui.DragFloat2("Position Offset##focus", ref TweakConfig.FocusOffset);
                ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
                hasChanged |= ImGui.SliderInt("Font Size##focus", ref TweakConfig.FocusFontSize, 8, 30);
            }
        }

        if (hasChanged || rebuild) SaveConfig(TweakConfig);
        if (rebuild) FreeAllNodes();
    }

    private bool DrawCombo(ref NodePosition setting, string label) {
        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo(label, setting.ToString())) {
            foreach (var direction in Enum.GetValues<NodePosition>()) {
                if (ImGui.Selectable(direction.ToString(), setting == direction)) {
                    setting = direction;
                    ImGui.EndCombo();
                    return true;
                }
            }
            
            ImGui.EndCombo();
        }

        return false;
    }

    protected override void Disable() {
        FreeAllNodes();
    }

    public override void Dispose() {
        stringBuffer->Dtor(true);
        base.Dispose();
    }

    [AddonPreDraw("_TargetInfoCastBar", "_TargetInfo", "_FocusTargetInfo")]
    private void OnAddonPreDraw(AddonArgs args) {
        if (Service.ClientState.IsPvP) return;
        
        var addon = (AtkUnitBase*) args.Addon;

        switch (args.AddonName) {
            case "_TargetInfoCastBar" when addon->IsVisible && TweakConfig.PrimaryTargetEnabled:
                UpdateAddon(addon, 7, 2, Service.Targets.Target);
                break;

            case "_TargetInfo" when addon->IsVisible && TweakConfig.PrimaryTargetEnabled:
                UpdateAddon(addon, 15, 10, Service.Targets.Target);
                break;

            case "_FocusTargetInfo" when addon->IsVisible && TweakConfig.FocusTargetEnabled:
                UpdateAddon(addon, 8, 3, Service.Targets.FocusTarget, true);
                break;
        }
    }

    private void UpdateAddon(AtkUnitBase* addon, uint visibilityNodeId, uint positioningNodeId, IGameObject? target, bool focusTarget = false) {
        var interruptNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, visibilityNodeId);
        var castBarNode = Common.GetNodeByID(&addon->UldManager, positioningNodeId);
        if (interruptNode is not null && castBarNode is not null) {
            TryMakeNodes(addon);
            UpdateIcons(interruptNode->IsVisible(), addon, target, castBarNode, focusTarget);
        }
    }

    private void TryMakeNodes(AtkUnitBase* parent) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastBarTextNodeId);
        if (textNode is null) MakeTextNode(parent, CastBarTextNodeId);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, IGameObject? target, AtkResNode* positioningNode, bool focusTarget) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastBarTextNodeId);
        if (textNode is null) return;
        
        if (target is IBattleChara targetInfo && castBarVisible && targetInfo.TotalCastTime > targetInfo.CurrentCastTime) {
            textNode->AtkResNode.ToggleVisibility(true);
            stringBuffer->SetString((targetInfo.TotalCastTime - targetInfo.CurrentCastTime).ToString("00.00", Culture));
            textNode->SetText(stringBuffer->StringPtr);
            textNode->FontSize = (byte) Math.Clamp(focusTarget ? TweakConfig.FocusFontSize : TweakConfig.FontSize, 8, 30);
            
            var nodePosition = (focusTarget ? TweakConfig.FocusTargetPosition : TweakConfig.CastbarPosition) switch {
                NodePosition.Left => new Vector2(positioningNode->X - 80, positioningNode->Y),
                NodePosition.Right => new Vector2(positioningNode->X + positioningNode->Width, positioningNode->Y),
                NodePosition.TopLeft => new Vector2(positioningNode->X, positioningNode->Y - 14),
                NodePosition.TopRight => new Vector2(positioningNode->X + positioningNode->Width - 80, positioningNode->Y - 14),
                NodePosition.BottomLeft => new Vector2(positioningNode->X, positioningNode->Y + 14),
                NodePosition.BottomRight => new Vector2(positioningNode->X + positioningNode->Width - 80, positioningNode->Y + 14),
                _ => Vector2.Zero
            } + (focusTarget ? TweakConfig.FocusOffset : TweakConfig.Offset);
        
            textNode->AtkResNode.SetPositionFloat(nodePosition.X, nodePosition.Y);
        }
        else {
            textNode->AtkResNode.ToggleVisibility(false);
        }
    }
    
    private void MakeTextNode(AtkUnitBase* parent, uint nodeId) {
        var textNode = UiHelper.MakeTextNode(nodeId);
        
        textNode->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
        textNode->DrawFlags = 2;
        textNode->Alpha_2 = 255;
        
        textNode->TextColor = textColor;
        textNode->EdgeColor = edgeColor;
        textNode->BackgroundColor = backgroundColor;
        textNode->LineSpacing = 20;
        textNode->AlignmentFontType = 37;
        textNode->FontSize = 20;
        textNode->TextFlags = (byte) TextFlags.Edge;
        
        textNode->SetWidth(80);
        textNode->SetHeight(22);

        UiHelper.LinkNodeAtEnd((AtkResNode*) textNode, parent);
    }
    
    private void FreeAllNodes() {
        var addonTargetInfoCastBar = Common.GetUnitBase("_TargetInfoCastBar");
        var addonTargetInfo = Common.GetUnitBase("_TargetInfo");
        var addonFocusTargetInfo = Common.GetUnitBase("_FocusTargetInfo");

        TryFreeTextNode(addonTargetInfoCastBar, CastBarTextNodeId);
        TryFreeTextNode(addonTargetInfo, CastBarTextNodeId);
        TryFreeTextNode(addonFocusTargetInfo, CastBarTextNodeId);
    }

    private void TryFreeTextNode(AtkUnitBase* addon, uint nodeId) {
        if (addon == null) return;

        var textNode = Common.GetNodeByID<AtkTextNode>(&addon->UldManager, nodeId);
        if (textNode is not null) {
            UiHelper.UnlinkAndFreeTextNode(textNode, addon);
        }
    }
}
