#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics;
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
public unsafe class TargetCastbarCountdown : UiAdjustments.SubTweak {
    private uint CastBarTextNodeId => CustomNodes.Get(this, "Countdown");

    private readonly ByteColor textColor = new() { R = 255, G = 255, B = 255, A = 255 };
    private readonly ByteColor edgeColor = new() { R = 142, G = 106, B = 12, A = 255 };
    private readonly ByteColor backgroundColor = new() { R = 0, G = 0, B = 0, A = 0 };

    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig {
        public bool PrimaryTargetEnabled = true;
        public bool FocusTargetEnabled = false;
        public NodePosition FocusTargetPosition = NodePosition.Left;
        public NodePosition CastbarPosition = NodePosition.BottomLeft;
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
        var hasChanged = ImGui.Checkbox("Enable Primary Target", ref TweakConfig.PrimaryTargetEnabled);

        hasChanged |= ImGui.Checkbox("Enable Focus Target", ref TweakConfig.FocusTargetEnabled);

        ImGui.TextUnformatted("Select which direction relative to Cast Bar to show countdown");
        if (TweakConfig is { PrimaryTargetEnabled: false, FocusTargetEnabled: false }) {
            ImGuiHelpers.ScaledIndent(20.0f);
            ImGui.TextUnformatted("No CastBars Selected");
            ImGuiHelpers.ScaledIndent(-20.0f);
        }
        
        if (TweakConfig.FocusTargetEnabled) {
            hasChanged |= DrawCombo(ref TweakConfig.FocusTargetPosition, "Focus Target");
        }

        if (TweakConfig.PrimaryTargetEnabled) {
            hasChanged |= DrawCombo(ref TweakConfig.CastbarPosition, "Primary Target");
        }

        if (hasChanged) {
            SaveConfig(TweakConfig);
            FreeAllNodes();
        }
    }

    private bool DrawCombo(ref NodePosition setting, string label) {
        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 1.0f / 3.0f);
        if (ImGui.BeginCombo(label, setting.ToString())) {
            foreach (var direction in Enum.GetValues<NodePosition>()) {
                if (ImGui.Selectable(direction.ToString(), setting == direction)) {
                    setting = direction;
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

    [AddonPostRequestedUpdate("_TargetInfoCastBar", "_TargetInfo", "_FocusTargetInfo")]
    private void OnAddonRequestedUpdate(AddonArgs args) {
        if (Service.ClientState.IsPvP) return;
        
        var addon = (AtkUnitBase*) args.Addon;

        switch (args.AddonName) {
            case "_TargetInfoCastBar" when addon->IsVisible:
                UpdateAddon(addon, 7, 2, Service.Targets.Target);
                break;

            case "_TargetInfo" when addon->IsVisible:
                UpdateAddon(addon, 15, 10, Service.Targets.Target);
                break;

            case "_FocusTargetInfo" when addon->IsVisible:
                UpdateAddon(addon, 8, 3, Service.Targets.FocusTarget, true);
                break;
        }
    }

    private void UpdateAddon(AtkUnitBase* addon, uint visibilityNodeId, uint positioningNodeId, GameObject? target, bool focusTarget = false) {
        var interruptNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, visibilityNodeId);
        var castBarNode = Common.GetNodeByID(&addon->UldManager, positioningNodeId);
        if (interruptNode is not null && castBarNode is not null) {
            TryMakeNodes(addon, castBarNode, focusTarget);
            UpdateIcons(interruptNode->AtkResNode.IsVisible, addon, target);
        }
    }

    private void TryMakeNodes(AtkUnitBase* parent, AtkResNode* positionNode, bool focusTarget) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastBarTextNodeId);
        if (textNode is null) MakeTextNode(parent, CastBarTextNodeId, positionNode, focusTarget);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, GameObject? target) {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastBarTextNodeId);
        if (textNode is null) return;
        
        if (target as BattleChara is { IsCasting: true } targetInfo && castBarVisible && targetInfo.TotalCastTime > targetInfo.CurrentCastTime) {
            textNode->AtkResNode.ToggleVisibility(true);
            textNode->SetText($"{targetInfo.TotalCastTime - targetInfo.CurrentCastTime:00.00}");
        }
        else {
            textNode->AtkResNode.ToggleVisibility(false);
        }
    }
    
    private void MakeTextNode(AtkUnitBase* parent, uint nodeId, AtkResNode* positioningNode, bool focusTarget) {
        var textNode = UiHelper.MakeTextNode(nodeId);
        
        textNode->AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
        textNode->AtkResNode.DrawFlags = 2;
        textNode->AtkResNode.DrawFlags = 2;
        textNode->AtkResNode.Alpha_2 = 255;
        
        textNode->TextColor = textColor;
        textNode->EdgeColor = edgeColor;
        textNode->BackgroundColor = backgroundColor;
        textNode->LineSpacing = 20;
        textNode->AlignmentFontType = 37;
        textNode->FontSize = 20;
        textNode->TextFlags = (byte) TextFlags.Edge;
        
        textNode->AtkResNode.SetWidth(80);
        textNode->AtkResNode.SetHeight(22);

        var nodePosition = (focusTarget ? TweakConfig.FocusTargetPosition : TweakConfig.CastbarPosition) switch {
            NodePosition.Left => new Vector2(positioningNode->X - 80, positioningNode->Y),
            NodePosition.Right => new Vector2(positioningNode->X + positioningNode->Width, positioningNode->Y),
            NodePosition.TopLeft => new Vector2(positioningNode->X, positioningNode->Y - 14),
            NodePosition.TopRight => new Vector2(positioningNode->X + positioningNode->Width - 80, positioningNode->Y - 14),
            NodePosition.BottomLeft => new Vector2(positioningNode->X, positioningNode->Y + 14),
            NodePosition.BottomRight => new Vector2(positioningNode->X + positioningNode->Width - 80, positioningNode->Y + 14),
            _ => Vector2.Zero
        };
        
        textNode->AtkResNode.SetPositionFloat(nodePosition.X, nodePosition.Y);

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