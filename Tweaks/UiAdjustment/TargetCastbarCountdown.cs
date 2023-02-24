#nullable enable
using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class TargetCastbarCountdown : UiAdjustments.SubTweak
{
    public override string Name => "Target Castbar Countdown";
    public override string Description => "Displays time remaining on targets ability cast.";
    protected override string Author => "MidoriKami";

    private static AtkUnitBase* AddonTargetInfoCastBar => Common.GetUnitBase("_TargetInfoCastBar");
    private static AtkUnitBase* AddonTargetInfo => Common.GetUnitBase("_TargetInfo");
    private static AtkUnitBase* AddonFocusTargetInfo => Common.GetUnitBase("_FocusTargetInfo");

    private const uint CastbarTextNodeId = 3000U;
    
    private readonly ByteColor textColor = new() { R = 255, G = 255, B = 255, A = 255 };
    private readonly ByteColor edgeColor = new() { R = 142, G = 106, B = 12, A = 255 };
    private readonly ByteColor backgroundColor = new() { R = 0, G = 0, B = 0, A = 0 };
    
    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig
    {
        public bool FocusTargetEnabled;
        public NodePosition FocusTargetPosition = NodePosition.Left;
        
        public NodePosition CastbarPosition = NodePosition.BottomRight;
    }

    private enum NodePosition
    {
        Right,
        Left, 
        TopLeft,
        BottomLeft,
        BottomRight
    }
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        ImGui.Checkbox("Enable Focus Target", ref TweakConfig.FocusTargetEnabled);

        ImGui.TextUnformatted("Select which direction relative to Cast Bar to show countdown");

        if (TweakConfig.FocusTargetEnabled)
        {
            DrawCombo(ref TweakConfig.FocusTargetPosition, "Focus Target");
        }
        
        DrawCombo(ref TweakConfig.CastbarPosition, "Target");
    };

    private void DrawCombo(ref NodePosition setting, string label)
    {
        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 1.0f / 3.0f);
        if (ImGui.BeginCombo(label, setting.ToString()))
        {
            foreach (var direction in Enum.GetValues<NodePosition>())
            {
                if (ImGui.Selectable(direction.ToString(), setting == direction))
                {
                    setting = direction;
                    SaveConfig(TweakConfig);
                    FreeAllNodes();
                }
            }
            
            ImGui.EndCombo();
        }
    }
    
    public override void Setup()
    {
        AddChangelogNewTweak(Changelog.UnreleasedVersion).Author("MidoriKami");
        base.Setup();
    }

    public override void Enable()
    {
        Common.FrameworkUpdate += OnFrameworkUpdate;
        TweakConfig = LoadConfig<Config>() ?? new Config();
        Service.ClientState.EnterPvP += OnEnterPvP;
        Service.ClientState.LeavePvP += OnLeavePvP;
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        Service.ClientState.EnterPvP -= OnEnterPvP;
        Service.ClientState.LeavePvP -= OnLeavePvP;
        FreeAllNodes();
        base.Disable();
    }

    public override void Dispose()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        Service.ClientState.EnterPvP -= OnEnterPvP;
        Service.ClientState.LeavePvP -= OnLeavePvP;
        FreeAllNodes();
        base.Dispose();
    }

    private void OnEnterPvP()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
    }
    
    private void OnLeavePvP()
    {
        Common.FrameworkUpdate += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate()
    {
        // Castbar is split from target info
        if (AddonTargetInfoCastBar is not null && AddonTargetInfoCastBar->IsVisible) UpdateAddon(AddonTargetInfoCastBar, 7, 2, Service.Targets.Target);

        // Castbar is combined with target info
        if (AddonTargetInfo is not null && AddonTargetInfo->IsVisible) UpdateAddon(AddonTargetInfo, 15, 10, Service.Targets.Target);

        // Focus target castbar
        if (AddonFocusTargetInfo is not null && AddonFocusTargetInfo->IsVisible && TweakConfig.FocusTargetEnabled) UpdateAddon(AddonFocusTargetInfo, 8, 3, Service.Targets.FocusTarget, true);
    }

    private void UpdateAddon(AtkUnitBase* addon, uint visibilityNodeId, uint positioningNodeId, GameObject? target, bool focusTarget = false)
    {
        var interruptNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, visibilityNodeId);
        var castbarNode = Common.GetNodeByID(&addon->UldManager, positioningNodeId);
        if (interruptNode is not null && castbarNode is not null)
        {
            TryMakeNodes(addon, castbarNode, focusTarget);
            UpdateIcons(interruptNode->AtkResNode.IsVisible, addon, target);
        }
    }

    private void TryMakeNodes(AtkUnitBase* parent, AtkResNode* positionNode, bool focusTarget)
    {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastbarTextNodeId);
        if (textNode is null) MakeTextNode(parent, CastbarTextNodeId, positionNode, focusTarget);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, GameObject? target)
    {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastbarTextNodeId);

        if (textNode is null) return;
        
        if (target as BattleChara is { IsCasting: true } targetInfo && castBarVisible)
        {
            textNode->AtkResNode.ToggleVisibility(true);
            textNode->SetText($"{targetInfo.TotalCastTime - targetInfo.CurrentCastTime:00.00}");
        }
        else
        {
            textNode->AtkResNode.ToggleVisibility(false);
        }
    }
    
    private void MakeTextNode(AtkUnitBase* parent, uint nodeId, AtkResNode* positioningNode, bool focusTarget)
    {
        var textNode = UiHelper.MakeTextNode(nodeId);
        textNode->TextColor = textColor;
        textNode->EdgeColor = edgeColor;
        textNode->BackgroundColor = backgroundColor;
        textNode->LineSpacing = 20;
        textNode->AlignmentFontType = 37;
        textNode->FontSize = 20;
        textNode->TextFlags = 8;

        var resNode = (AtkResNode*) textNode;

        resNode->Flags = 8243;
        resNode->Flags_2 = 2;
        resNode->DrawFlags = 2;
        resNode->Alpha_2 = 255;
        resNode->SetWidth(80);
        resNode->SetHeight(22);

        switch (focusTarget ? TweakConfig.FocusTargetPosition : TweakConfig.CastbarPosition)
        {
            case NodePosition.Left:
                textNode->AtkResNode.SetPositionFloat(positioningNode->X - 80, positioningNode->Y);
                break;
            
            case NodePosition.Right:
                textNode->AtkResNode.SetPositionFloat(positioningNode->X + positioningNode->Width, positioningNode->Y);
                break;
            
            case NodePosition.TopLeft:
                textNode->AtkResNode.SetPositionFloat(positioningNode->X, positioningNode->Y - 14);
                break;
            
            case NodePosition.BottomLeft:
                textNode->AtkResNode.SetPositionFloat(positioningNode->X, positioningNode->Y + 14);
                break;
            
            case NodePosition.BottomRight:
                textNode->AtkResNode.SetPositionFloat(positioningNode->X + positioningNode->Width - 80, positioningNode->Y + 14);
                break;
        }
        
        UiHelper.LinkNodeAtEnd((AtkResNode*) textNode, parent);
    }
    
    private void FreeAllNodes()
    {
        TryFreeTextNode(AddonTargetInfoCastBar, CastbarTextNodeId);
        TryFreeTextNode(AddonTargetInfo, CastbarTextNodeId);
        TryFreeTextNode(AddonFocusTargetInfo, CastbarTextNodeId);
    }

    private void TryFreeTextNode(AtkUnitBase* addon, uint nodeId)
    {
        var textNode = Common.GetNodeByID<AtkTextNode>(&addon->UldManager, nodeId);
        if (textNode is not null)
        {
            UiHelper.UnlinkAndFreeTextNode(textNode, addon);
        }
    }
}