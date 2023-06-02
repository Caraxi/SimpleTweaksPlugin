using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class DutyListBackground : UiAdjustments.SubTweak
{
    public override string Name => "Duty List Background";
    public override string Description => "Adds a configurable background to the Duty List";
    protected override string Author => "MidoriKami";

    private static AtkUnitBase* AddonToDoList => Common.GetUnitBase<AtkUnitBase>("_ToDoList");
    private const uint ImageNodeId = 1000;

    public class Config : TweakConfig
    {
        public Vector4 BackgroundColor = new(0.0f, 0.0f, 0.0f, 0.40f);
    }
    
    public Config TweakConfig { get; private set; } = null!;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool configChanged) =>
    {
        if (ImGui.ColorEdit4($"Background Color##ColorEdit", ref TweakConfig.BackgroundColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
        {
            configChanged = true;
        }
    };

    protected override void ConfigChanged()
    {
        SaveConfig(TweakConfig);
        
        if (!UiHelper.IsAddonReady(AddonToDoList)) return;
        
        var imageNode = Common.GetNodeByID<AtkImageNode>(&AddonToDoList->UldManager, ImageNodeId);
        if (imageNode is not null)
        {
            UpdateBackgroundColor(imageNode);
        }
    }

    public override void Setup()
    {
        if (Ready) return;

        AddChangelogNewTweak("1.8.7.0");
        AddChangelog("1.8.7.1", "Improved tweak stability");
        
        Ready = true;
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

        TryRemoveNode();

        base.Disable();
    }
    
    private void OnFrameworkUpdate()
    {
        if (!UiHelper.IsAddonReady(AddonToDoList)) return;

        // If the current area is an inn, remove the node
        if (Service.Data.GetExcelSheet<TerritoryType>()!.GetRow(Service.ClientState.TerritoryType)?.TerritoryIntendedUse is 2)
        {
            TryRemoveNode();
            return;
        }

        var imageNode = Common.GetNodeByID<AtkImageNode>(&AddonToDoList->UldManager, ImageNodeId);
        
        if(imageNode is null)
        {
            imageNode = AllocateImageNode();
            UiHelper.LinkNodeAtEnd((AtkResNode*)imageNode, AddonToDoList);
        }
        else
        {
            imageNode->AtkResNode.SetWidth(AddonToDoList->RootNode->Width);
            imageNode->AtkResNode.SetHeight(AddonToDoList->RootNode->Height);
            imageNode->AtkResNode.SetPositionFloat(0.1f, 0);
        }
    }
    
    private void TryRemoveNode()
    {
        if (!UiHelper.IsAddonReady(AddonToDoList)) return;

        var imageNode = Common.GetNodeByID<AtkImageNode>(&AddonToDoList->UldManager, ImageNodeId);
        if (imageNode is not null)
        {
            UiHelper.UnlinkAndFreeImageNode(imageNode, AddonToDoList);
        }
    }

    private AtkImageNode* AllocateImageNode()
    {
        var imageNode = UiHelper.MakeImageNode(ImageNodeId, new UiHelper.PartInfo(0, 0, 0, 0));
        imageNode->AtkResNode.Flags = (short) (NodeFlags.Enabled | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.UseDepthBasedPriority);
        imageNode->WrapMode = 1;
        imageNode->Flags = 0;

        UpdateBackgroundColor(imageNode);

        return imageNode;
    }
    
    private void UpdateBackgroundColor(AtkImageNode* imageNode)
    {
        if (imageNode is not null)
        {
            imageNode->AtkResNode.Color.A = (byte) (TweakConfig.BackgroundColor.W * 255);
            imageNode->AtkResNode.AddRed = (byte) (TweakConfig.BackgroundColor.X * 255);
            imageNode->AtkResNode.AddGreen = (byte) (TweakConfig.BackgroundColor.Y * 255);
            imageNode->AtkResNode.AddBlue = (byte) (TweakConfig.BackgroundColor.Z * 255);
        }
    }
}