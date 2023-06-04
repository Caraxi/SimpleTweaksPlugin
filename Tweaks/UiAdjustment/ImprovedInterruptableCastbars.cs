#nullable enable
using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class ImprovedInterruptableCastbars : UiAdjustments.SubTweak
{
    public override string Name => "Improved Interruptable Castbars";
    public override string Description => "Displays an icon next to interruptable castbars";
    protected override string Author => "MidoriKami";

    private static AtkUnitBase* AddonTargetInfoCastBar => Common.GetUnitBase("_TargetInfoCastBar");
    private static AtkUnitBase* AddonTargetInfo => Common.GetUnitBase("_TargetInfo");
    private static AtkUnitBase* AddonFocusTargetInfo => Common.GetUnitBase("_FocusTargetInfo");
    
    private const uint InterjectImageNodeId = 1000U;
    private const uint HeadGrazeImageNodeId = 2000U;

    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig
    {
        public NodePosition Position = NodePosition.TopLeft;
    }

    private enum NodePosition
    {
        Left,
        Right,
        TopLeft
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        ImGui.TextUnformatted("Select which direction relative to Cast Bar to show interrupt icon");
        
        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 1.0f / 3.0f);
        if (ImGui.BeginCombo("Direction", TweakConfig.Position.ToString()))
        {
            foreach (var direction in Enum.GetValues<NodePosition>())
            {
                if (ImGui.Selectable(direction.ToString(), TweakConfig.Position == direction))
                {
                    TweakConfig.Position = direction;
                    SaveConfig(TweakConfig);
                    FreeAllNodes();
                }
            }
            
            ImGui.EndCombo();
        }
    };
    
    public override void Setup()
    {
        AddChangelogNewTweak("1.8.3.0");
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Disable();
    }
    
    private void OnFrameworkUpdate()
    {
        // Castbar is split from target info
        if (UiHelper.IsAddonReady(AddonTargetInfoCastBar) && AddonTargetInfoCastBar->IsVisible) UpdateAddon(AddonTargetInfoCastBar, 6, 2, Service.Targets.Target);

        // Castbar is combined with target info
        if (UiHelper.IsAddonReady(AddonTargetInfo) && AddonTargetInfo->IsVisible) UpdateAddon(AddonTargetInfo, 14, 10, Service.Targets.Target);

        // Focus target castbar
        if (UiHelper.IsAddonReady(AddonFocusTargetInfo) && AddonFocusTargetInfo->IsVisible) UpdateAddon(AddonFocusTargetInfo, 7, 3, Service.Targets.FocusTarget);
    }

    private void UpdateAddon(AtkUnitBase* addon, uint interruptNodeId, uint positioningNodeId, GameObject? target)
    {
        if (!UiHelper.IsAddonReady(addon)) return;
        
        var interruptNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, interruptNodeId);
        var castbarNode = Common.GetNodeByID(&addon->UldManager, positioningNodeId);
        if (interruptNode is not null && castbarNode is not null)
        {
            TryMakeNodes(addon, castbarNode);
            UpdateIcons(interruptNode->AtkResNode.IsVisible, addon, target);
        }
    }
    
    private void TryMakeNodes(AtkUnitBase* parent, AtkResNode* positionNode)
    {
        if (!UiHelper.IsAddonReady(parent)) return;
        
        var interject = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, InterjectImageNodeId);
        if (interject is null) MakeImageNode(parent, InterjectImageNodeId, 808, positionNode);

        var headGraze = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, HeadGrazeImageNodeId);
        if(headGraze is null) MakeImageNode(parent, HeadGrazeImageNodeId, 848, positionNode);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, GameObject? target)
    {
        if (!UiHelper.IsAddonReady(parent)) return;
        
        var interject = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, InterjectImageNodeId);
        var headGraze = Common.GetNodeByID<AtkImageNode>(&parent->UldManager, HeadGrazeImageNodeId);

        if (interject is null || headGraze is null) return;
        
        if (target as BattleChara is { IsCasting: true, IsCastInterruptible: true } && castBarVisible)
        {
            switch (Service.ClientState.LocalPlayer)
            {
                // Tank
                case { ClassJob.GameData.Role: 1, Level: >= 18 }:
                    interject->AtkResNode.ToggleVisibility(true);
                    headGraze->AtkResNode.ToggleVisibility(false);
                    break;

                // Physical Ranged
                case { ClassJob.GameData.UIPriority: >= 30 and <= 39, Level: >= 24 }:
                    interject->AtkResNode.ToggleVisibility(false);
                    headGraze->AtkResNode.ToggleVisibility(true);
                    break;
            }
        }
        else
        {
            interject->AtkResNode.ToggleVisibility(false);
            headGraze->AtkResNode.ToggleVisibility(false);
        }
    }

    private void MakeImageNode(AtkUnitBase* parent, uint nodeId, int icon, AtkResNode* positioningNode)
    {
        if (!UiHelper.IsAddonReady(parent)) return;
        
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 36, 36));
        imageNode->AtkResNode.Flags = 8243;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;
        
        imageNode->LoadIconTexture(icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);

        switch (TweakConfig.Position)
        {
            case NodePosition.Left:
                imageNode->AtkResNode.SetPositionFloat(positioningNode->X - 36, positioningNode->Y - 8);
                break;
            
            case NodePosition.Right:
                imageNode->AtkResNode.SetPositionFloat(positioningNode->X + positioningNode->Width, positioningNode->Y - 8);
                break;
            
            case NodePosition.TopLeft:
                imageNode->AtkResNode.SetPositionFloat(positioningNode->X, positioningNode->Y - 36);
                break;
        }
        
        UiHelper.LinkNodeAtEnd((AtkResNode*) imageNode, parent);
    }

    private void FreeAllNodes()
    {
        TryFreeImageNode(AddonTargetInfoCastBar, InterjectImageNodeId);
        TryFreeImageNode(AddonTargetInfoCastBar, HeadGrazeImageNodeId);
        
        TryFreeImageNode(AddonTargetInfo, InterjectImageNodeId);
        TryFreeImageNode(AddonTargetInfo, HeadGrazeImageNodeId);
        
        TryFreeImageNode(AddonFocusTargetInfo, InterjectImageNodeId);
        TryFreeImageNode(AddonFocusTargetInfo, HeadGrazeImageNodeId);
    }
    
    private void TryFreeImageNode(AtkUnitBase* addon, uint nodeId)
    {
        if (!UiHelper.IsAddonReady(addon)) return;
        
        var imageNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, nodeId);
        if (imageNode is not null)
        {
            UiHelper.UnlinkAndFreeImageNode(imageNode, addon);
        }
    }
}