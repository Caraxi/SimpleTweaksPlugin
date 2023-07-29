#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
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
        public bool PrimaryTargetEnabled = true;
        public bool FocusTargetEnabled = false;
        public NodePosition FocusTargetPosition = NodePosition.Left;
        public NodePosition CastbarPosition = NodePosition.BottomLeft;
    }

    private enum NodePosition
    {
        Right,
        Left, 
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    protected override void ConfigChanged()
    {
        SaveConfig(TweakConfig);
        FreeAllNodes();
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
    {
        if (ImGui.Checkbox("Enable Primary Target", ref TweakConfig.PrimaryTargetEnabled)) hasChanged = true;

        if (ImGui.Checkbox("Enable Focus Target", ref TweakConfig.FocusTargetEnabled)) hasChanged = true;

        ImGui.TextUnformatted("Select which direction relative to Cast Bar to show countdown");
        if (TweakConfig is { PrimaryTargetEnabled: false, FocusTargetEnabled: false })
        {
            ImGuiHelpers.ScaledIndent(20.0f);
            ImGui.TextUnformatted("No Castbars Selected");
            ImGuiHelpers.ScaledIndent(-20.0f);
        }
        
        if (TweakConfig.FocusTargetEnabled)
        {
            if (DrawCombo(ref TweakConfig.FocusTargetPosition, "Focus Target")) hasChanged = true;
        }

        if (TweakConfig.PrimaryTargetEnabled)
        {
            if (DrawCombo(ref TweakConfig.CastbarPosition, "Primary Target")) hasChanged = true;
        }
    };

    private bool DrawCombo(ref NodePosition setting, string label)
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
                    return true;
                }
            }
            
            ImGui.EndCombo();
        }

        return false;
    }
    
    public override void Setup()
    {
        AddChangelogNewTweak("1.8.3.0");
        AddChangelog("1.8.3.1", "Add TopRight option for displaying countdown");
        AddChangelog("1.8.9.0", "Add option to disable on primary target");
        base.Setup();
    }

    protected override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        Common.FrameworkUpdate += OnFrameworkUpdate;
        Service.ClientState.EnterPvP += OnEnterPvP;
        Service.ClientState.LeavePvP += OnLeavePvP;
        base.Enable();
    }

    protected override void Disable()
    {
        SaveConfig(TweakConfig);
        
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        Service.ClientState.EnterPvP -= OnEnterPvP;
        Service.ClientState.LeavePvP -= OnLeavePvP;
        
        FreeAllNodes();
        
        base.Disable();
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
        // If we get here while in any kind of PvP area, unregister this callback and free nodes.
        if (Service.ClientState.IsPvP)
        {
            Common.FrameworkUpdate -= OnFrameworkUpdate;
            FreeAllNodes();
            return;
        }
        
        // Castbar is split from target info
        if (AddonTargetInfoCastBar is not null && AddonTargetInfoCastBar->IsVisible && TweakConfig.PrimaryTargetEnabled) UpdateAddon(AddonTargetInfoCastBar, 7, 2, Service.Targets.Target);

        // Castbar is combined with target info
        if (AddonTargetInfo is not null && AddonTargetInfo->IsVisible && TweakConfig.PrimaryTargetEnabled) UpdateAddon(AddonTargetInfo, 15, 10, Service.Targets.Target);

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
        if (!UiHelper.IsAddonReady(parent)) return;
        
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastbarTextNodeId);
        if (textNode is null) MakeTextNode(parent, CastbarTextNodeId, positionNode, focusTarget);
    }
    
    private void UpdateIcons(bool castBarVisible, AtkUnitBase* parent, GameObject? target)
    {
        var textNode = Common.GetNodeByID<AtkTextNode>(&parent->UldManager, CastbarTextNodeId);

        if (textNode is null) return;
        
        if (target as BattleChara is { IsCasting: true } targetInfo && castBarVisible && targetInfo.TotalCastTime > targetInfo.CurrentCastTime)
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
        
        textNode->AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
        textNode->AtkResNode.Flags_2 = 2;
        textNode->AtkResNode.DrawFlags = 2;
        textNode->AtkResNode.Alpha_2 = 255;
        
        textNode->TextColor = textColor;
        textNode->EdgeColor = edgeColor;
        textNode->BackgroundColor = backgroundColor;
        textNode->LineSpacing = 20;
        textNode->AlignmentFontType = 37;
        textNode->FontSize = 20;
        textNode->TextFlags = (byte) (TextFlags.Edge);
        
        textNode->AtkResNode.SetWidth(80);
        textNode->AtkResNode.SetHeight(22);

        var nodePosition = (focusTarget ? TweakConfig.FocusTargetPosition : TweakConfig.CastbarPosition) switch
        {
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
    
    private void FreeAllNodes()
    {
        TryFreeTextNode(AddonTargetInfoCastBar, CastbarTextNodeId);
        TryFreeTextNode(AddonTargetInfo, CastbarTextNodeId);
        TryFreeTextNode(AddonFocusTargetInfo, CastbarTextNodeId);
    }

    private void TryFreeTextNode(AtkUnitBase* addon, uint nodeId) {
        if (addon == null) return;
        var textNode = Common.GetNodeByID<AtkTextNode>(&addon->UldManager, nodeId);
        if (textNode is not null)
        {
            UiHelper.UnlinkAndFreeTextNode(textNode, addon);
        }
    }
}