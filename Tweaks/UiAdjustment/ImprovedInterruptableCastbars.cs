using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
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

    private static AtkUnitBase* AddonCastBar => Common.GetUnitBase("_TargetInfoCastBar");
    private static AtkUnitBase* AddonTargetInfo => Common.GetUnitBase("_TargetInfo");
    
    private const uint InterjectImageNodeId = 1000U;
    private const uint HeadGrazeImageNodeId = 2000U;

    private Config TweakConfig { get; set; } = null!;

    private class Config : TweakConfig
    {
        public bool PreviewPosition;
        public int OffsetX;
        public int OffsetY;
    }
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        var regionSize = ImGui.GetContentRegionAvail();
        
        ImGui.Text("Position Offset for Icon");

        ImGui.TextUnformatted("( X , Y ) Position:");
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(regionSize.X / 3.0f);
        if (ImGui.DragInt("##XCoordinate", ref TweakConfig.OffsetX, 1920 * 0.003f, -1920, 1920))
        {
            FreeAllNodes();
        }
        
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            SaveConfig(TweakConfig);
        }
        
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(regionSize.X / 3.0f);

        if (ImGui.DragInt("##YCoordinate", ref TweakConfig.OffsetY, 1920 * 0.003f, -1920, 1920))
        {
            FreeAllNodes();
        }
        
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            SaveConfig(TweakConfig);
        }
        ImGuiComponents.HelpMarker("You can Control + Click the slider to enter an exact value.");

        ImGui.Checkbox("Preview Position", ref TweakConfig.PreviewPosition);
        ImGuiComponents.HelpMarker("You must have a target selected to preview the icons. You can target self.");
    };
    
    public override void Setup()
    {
        AddChangelogNewTweak(Changelog.UnreleasedVersion).Author("MidoriKami");
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

    public override void Dispose()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Dispose();
    }
    
    private void OnFrameworkUpdate()
    {
        // The user can have the castBar split out
        if (AddonCastBar is not null)
        {
            UpdateCastBarNodes();
        }

        // Or combined with the target info element
        if (AddonTargetInfo is not null)
        {
            UpdateTargetInfoNodes();
        }
    }

    private void UpdateCastBarNodes()
    {
        // Use the inner actual castBar for sizing
        var containingResourceNode = Common.GetNodeByID<AtkImageNode>(&AddonCastBar->UldManager, 2);
        if (containingResourceNode is null) return;
        var castBarPosition = new Vector2(containingResourceNode->AtkResNode.X, containingResourceNode->AtkResNode.Y);

        // We want this node to make sure ours goes visible/not visible at the right time.
        var castBarImageNode = Common.GetNodeByID<AtkImageNode>(&AddonCastBar->UldManager, 7);

        MakeAndUpdateNodes(AddonCastBar, castBarPosition, castBarImageNode);
    }

    private void UpdateTargetInfoNodes()
    {
        // Use the inner actual castBar for sizing
        var containingResourceNode = Common.GetNodeByID<AtkImageNode>(&AddonTargetInfo->UldManager, 10);
        if (containingResourceNode is null) return;
        var castBarPosition = new Vector2(containingResourceNode->AtkResNode.X, containingResourceNode->AtkResNode.Y);

        // We want this node to make sure ours goes visible/not visible at the right time.
        var castBarImageNode = Common.GetNodeByID<AtkImageNode>(&AddonTargetInfo->UldManager, 15);

        MakeAndUpdateNodes(AddonTargetInfo, castBarPosition, castBarImageNode);
    }
    
    private void MakeAndUpdateNodes(AtkUnitBase* addon, Vector2 castBarPosition, AtkImageNode* castBarImageNode)
    {
        var interjectNode = Common.GetNodeByID(&AddonCastBar->UldManager, InterjectImageNodeId);
        var headGrazeNode = Common.GetNodeByID(&AddonCastBar->UldManager, HeadGrazeImageNodeId);

        if (interjectNode is null || headGrazeNode is null)
        {
            if (interjectNode is null)
            {
                var position = castBarPosition - new Vector2(36, 8);
                var offset = new Vector2(TweakConfig.OffsetX, TweakConfig.OffsetY);
                MakeImageNode(addon, InterjectImageNodeId, position + offset, 808);
            }

            if (headGrazeNode is null)
            {
                var position = castBarPosition - new Vector2(36, 8);
                var offset = new Vector2(TweakConfig.OffsetX, TweakConfig.OffsetY);
                MakeImageNode(addon, HeadGrazeImageNodeId, position + offset, 848);
            }
        }
        else // Image Nodes are valid
        {
            UpdateIcons(castBarImageNode, interjectNode, headGrazeNode);
        }
    }
    
    private void UpdateIcons(AtkImageNode* castBarImageNode, AtkResNode* interjectNode, AtkResNode* headGrazeNode)
    {
        if (TweakConfig.PreviewPosition)
        {
            interjectNode->ToggleVisibility(true);
            headGrazeNode->ToggleVisibility(true);
            return;
        }
        
        var castBarVisible = castBarImageNode != null && castBarImageNode->AtkResNode.IsVisible;

        if (Service.Targets.Target as BattleChara is { IsCasting: true, IsCastInterruptible: true } && castBarVisible)
        {
            switch (Service.ClientState.LocalPlayer)
            {
                // Tank
                case { ClassJob.GameData.Role: 1, Level: >= 18 }:
                    interjectNode->ToggleVisibility(true);
                    headGrazeNode->ToggleVisibility(false);
                    break;

                // Physical Ranged
                case { ClassJob.GameData.UIPriority: >= 30 and <= 39, Level: >= 24 }:
                    interjectNode->ToggleVisibility(false);
                    headGrazeNode->ToggleVisibility(true);
                    break;
            }
        }
        else
        {
            interjectNode->ToggleVisibility(false);
            headGrazeNode->ToggleVisibility(false);
        }
    }

    private void MakeImageNode(AtkUnitBase* addon, uint nodeId, Vector2 position, int icon)
    {
        var imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
        imageNode->AtkResNode.Type = NodeType.Image;
        imageNode->AtkResNode.NodeID = nodeId;
        imageNode->AtkResNode.Flags = 8243;
        imageNode->AtkResNode.DrawFlags = 0;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;
        
        var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        if (partsList == null) 
        {
            SimpleLog.Error("Failed to alloc memory for parts list.");
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }
        
        partsList->Id = 0;
        partsList->PartCount = 1;

        var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
        if (part == null) 
        {
            SimpleLog.Error("Failed to alloc memory for part.");
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }

        part->U = 0;
        part->V = 0;
        part->Width = 36;
        part->Height = 36;

        partsList->Parts = part;

        var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
        if (asset == null) 
        {
            SimpleLog.Error("Failed to alloc memory for asset.");
            IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }

        asset->Id = 0;
        asset->AtkTexture.Ctor();
        part->UldAsset = asset;
        imageNode->PartsList = partsList;
        
        imageNode->LoadIconTexture(icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);
        imageNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);
        
        var node = addon->RootNode->ChildNode;
        while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

        node->PrevSiblingNode = (AtkResNode*) imageNode;
        imageNode->AtkResNode.NextSiblingNode = node;
        imageNode->AtkResNode.ParentNode = node->ParentNode;
        
        addon->UldManager.UpdateDrawNodeList();
    }

    private void FreeAllNodes()
    {
        TryFreeImageNode(AddonCastBar, InterjectImageNodeId);
        TryFreeImageNode(AddonCastBar, HeadGrazeImageNodeId);
        
        TryFreeImageNode(AddonTargetInfo, InterjectImageNodeId);
        TryFreeImageNode(AddonTargetInfo, HeadGrazeImageNodeId);
    }
    
    private void TryFreeImageNode(AtkUnitBase* addon, uint nodeId)
    {
        var imageNode = Common.GetNodeByID(&addon->UldManager, nodeId);
        if (imageNode is not null)
        {
            FreeImageNode(addon, nodeId);
        }
    }
    
    private void FreeImageNode(AtkUnitBase* addon, uint nodeId)
    {
        var imageNode = Common.GetNodeByID<AtkImageNode>(&addon->UldManager, nodeId, NodeType.Image);
        if (imageNode != null)
        {
            if (imageNode->AtkResNode.PrevSiblingNode != null)
                imageNode->AtkResNode.PrevSiblingNode->NextSiblingNode = imageNode->AtkResNode.NextSiblingNode;
            
            if (imageNode->AtkResNode.NextSiblingNode != null)
                imageNode->AtkResNode.NextSiblingNode->PrevSiblingNode = imageNode->AtkResNode.PrevSiblingNode;
            
            addon->UldManager.UpdateDrawNodeList();

            IMemorySpace.Free(imageNode->PartsList->Parts->UldAsset, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(imageNode->PartsList->Parts, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(imageNode->PartsList, (ulong) sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
        }
    }
}