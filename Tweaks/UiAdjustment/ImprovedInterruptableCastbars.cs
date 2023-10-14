#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Improved Interruptable Castbars")]
[TweakDescription("Displays an icon next to interruptable castbars.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.3.0")]
public unsafe class ImprovedInterruptableCastbars : UiAdjustments.SubTweak{
    private const uint InterjectImageNodeId = 1000U;
    private const uint HeadGrazeImageNodeId = 2000U;

    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig {
        public NodePosition Position = NodePosition.TopLeft;
    }

    private enum NodePosition {
        Left,
        Right,
        TopLeft
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool configChanged) => {
        ImGui.TextUnformatted("Select which direction relative to Cast Bar to show interrupt icon");
        
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3.0f);
        if (ImGui.BeginCombo("Direction", TweakConfig.Position.ToString())) {
            foreach (var direction in Enum.GetValues<NodePosition>()) {
                if (ImGui.Selectable(direction.ToString(), TweakConfig.Position == direction)) {
                    TweakConfig.Position = direction;
                    configChanged = true;
                }
            }
            
            ImGui.EndCombo();
        }
    };

    protected override void ConfigChanged() {
        SaveConfig(TweakConfig);
        FreeAllNodes();
    }

    protected override void Enable() {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(TweakConfig);
        FreeAllNodes();
        base.Disable();
    }

    [AddonPostRequestedUpdate("_TargetInfoCastBar", "_TargetInfo", "_FocusTargetInfo")]
    private void OnAddonUpdate(AddonArgs args) {
        var addon = (AtkUnitBase*) args.Addon;
        
        switch (args.AddonName) {
            case "_TargetInfoCastBar" when addon->IsVisible:
                UpdateAddon(addon, 6, 2, Service.Targets.Target);
                break;
            
            case "_TargetInfo" when addon->IsVisible:
                UpdateAddon(addon, 14, 10, Service.Targets.Target);
                break;
            
            case "_FocusTargetInfo" when addon->IsVisible:
                UpdateAddon(addon, 7, 3, Service.Targets.FocusTarget);
                break;
        }
    }

    private void UpdateAddon(AtkUnitBase* addon, uint interruptNodeId, uint positioningNodeId, GameObject? target) {
        var interruptNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, interruptNodeId);
        var castBarNode = Common.GetNodeByID(&addon->UldManager, positioningNodeId);
        if (interruptNode is not null && castBarNode is not null) {
            TryMakeNodes(addon, castBarNode);
            UpdateIcons(interruptNode->AtkResNode.IsVisible, addon, target);
        }
    }
    
    private void TryMakeNodes(AtkUnitBase* parent, AtkResNode* positionNode) {
        var interject = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, InterjectImageNodeId);
        if (interject is null) MakeImageNode(parent, InterjectImageNodeId, 808, positionNode);
    
        var headGraze = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, HeadGrazeImageNodeId);
        if(headGraze is null) MakeImageNode(parent, HeadGrazeImageNodeId, 848, positionNode);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, GameObject? target) {
        var interject = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, InterjectImageNodeId);
        var headGraze = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, HeadGrazeImageNodeId);
    
        if (interject is null || headGraze is null) return;
        
        if (target as BattleChara is { IsCasting: true, IsCastInterruptible: true } && castBarVisible) {
            switch (Service.ClientState.LocalPlayer) {
                case { ClassJob.GameData.Role: 1, Level: >= 18 }: // Tank
                    interject->AtkResNode.ToggleVisibility(true);
                    headGraze->AtkResNode.ToggleVisibility(false);
                    break;
    
                case { ClassJob.GameData.UIPriority: >= 30 and <= 39, Level: >= 24 }: // Physical Ranged
                    interject->AtkResNode.ToggleVisibility(false);
                    headGraze->AtkResNode.ToggleVisibility(true);
                    break;
            }
        }
        else {
            interject->AtkResNode.ToggleVisibility(false);
            headGraze->AtkResNode.ToggleVisibility(false);
        }
    }
    
    private void MakeImageNode(AtkUnitBase* parent, uint nodeId, int icon, AtkResNode* positioningNode) {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 36, 36));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;
        
        imageNode->LoadIconTexture(icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);

        var position = TweakConfig.Position switch {
            NodePosition.Left => new Vector2(positioningNode->X - 36, positioningNode->Y - 8),
            NodePosition.Right => new Vector2(positioningNode->X + positioningNode->Width, positioningNode->Y - 8),
            NodePosition.TopLeft => new Vector2(positioningNode->X, positioningNode->Y - 36),
            _ => Vector2.Zero,
        };
        
        imageNode->AtkResNode.SetPositionFloat(position.X, position.Y);
        
        UiHelper.LinkNodeAtEnd((AtkResNode*) imageNode, parent);
    }

    private void FreeAllNodes() { 
        var addonTargetInfoCastBar = Common.GetUnitBase("_TargetInfoCastBar");
        var addonTargetInfo = Common.GetUnitBase("_TargetInfo");
        var addonFocusTargetInfo = Common.GetUnitBase("_FocusTargetInfo");
        
        TryFreeImageNode(addonTargetInfoCastBar, InterjectImageNodeId);
        TryFreeImageNode(addonTargetInfoCastBar, HeadGrazeImageNodeId);
        
        TryFreeImageNode(addonTargetInfo, InterjectImageNodeId);
        TryFreeImageNode(addonTargetInfo, HeadGrazeImageNodeId);
        
        TryFreeImageNode(addonFocusTargetInfo, InterjectImageNodeId);
        TryFreeImageNode(addonFocusTargetInfo, HeadGrazeImageNodeId);
    }
    
    private void TryFreeImageNode(AtkUnitBase* addon, uint nodeId) {
        if (!UiHelper.IsAddonReady(addon)) return;
        
        var imageNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, nodeId);
        if (imageNode is not null) {
            UiHelper.UnlinkAndFreeImageNode(imageNode, addon);
        }
    }
}