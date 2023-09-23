using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ShowShieldOnHPBar: UiAdjustments.SubTweak
{
    public override string Name => "Shield on HP Bar";
    public override string Description => "Show approximate shield on the HP Bar.";

    protected override string Author => "Chivalrik";

    public override void Setup() {
        AddChangelog("1.8.9.0", "Fixed tweak not working after relogging.");
        base.Setup();
    }

    public class Configs : TweakConfig {
        [TweakConfigOption("Shield Colour", "Color")]
        public Vector3 ShieldColour = Common.UiColorToVector3(0xFFFF00);
    }

    public Configs Config { get; private set; }
    public override bool UseAutoConfig => true;

    protected override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        Common.FrameworkUpdate += FrameworkUpdate;
        base.Enable();
    }

    protected override void Disable()
    {
        SaveConfig(Config);
        Common.FrameworkUpdate -= FrameworkUpdate;
        Finalize(Common.GetUnitBase("_ParameterWidget"));
        base.Disable();
    }

    private readonly uint shieldGridID = CustomNodes.Get("ShieldOnHP");
    private readonly uint overShieldGridID = CustomNodes.Get("OverShieldOnHP");

    [AddonFinalize("_ParameterWidget")]
    public void Finalize(AtkUnitBase* parameterWidget) {
        SimpleLog.Debug("Removing Nodes");
        if (parameterWidget == null) return;
        var hpGaugeBarNode = parameterWidget->GetNodeById(3);
        if (hpGaugeBarNode == null) return;
        var hpGaugeBar = hpGaugeBarNode->GetAsAtkComponentNode();
        if (hpGaugeBar == null) return;

        var shieldGrid = Common.GetNodeByID(&hpGaugeBar->Component->UldManager, shieldGridID);
        var overShieldGrid = Common.GetNodeByID(&hpGaugeBar->Component->UldManager, overShieldGridID);

        if (shieldGrid != null) {
            UiHelper.UnlinkNode(shieldGrid, hpGaugeBar);
            shieldGrid->Destroy(true);
        }

        if (overShieldGrid != null) {
            UiHelper.UnlinkNode(overShieldGrid, hpGaugeBar);
            overShieldGrid->Destroy(true);
        }
    }
    
    
    private void FrameworkUpdate()
    {           
        try
        {
            var parameterWidget = Common.GetUnitBase("_ParameterWidget");
            if (parameterWidget == null) return;
            if (parameterWidget->UldManager.LoadedState != AtkLoadState.Loaded) return;
            if (!parameterWidget->IsVisible) return;
            
            var hpGaugeBarNode = parameterWidget->GetNodeById(3);
            if (hpGaugeBarNode == null) return;
            var hpGaugeBar = hpGaugeBarNode->GetAsAtkComponentNode();
            if (hpGaugeBar == null) return;

            var shieldGrid = Common.GetNodeByID<AtkNineGridNode>(&hpGaugeBar->Component->UldManager, shieldGridID);
            var overShieldGrid = Common.GetNodeByID<AtkNineGridNode>(&hpGaugeBar->Component->UldManager, overShieldGridID);
            var hpNineGrid = Common.GetNodeByID<AtkNineGridNode>(&hpGaugeBar->Component->UldManager, 5);
            
            if (hpNineGrid == null) return;

            void CreateBar(uint id) {
                var bar = IMemorySpace.GetUISpace()->Create<AtkNineGridNode>();
                bar->Ctor();
                bar->AtkResNode.Type = NodeType.NineGrid;
                bar->AtkResNode.NodeID = id;
                bar->PartsList = hpNineGrid->PartsList;
                bar->TopOffset = 0;
                bar->BottomOffset = 0;
                bar->LeftOffset = 7;
                bar->RightOffset = 7;
                bar->BlendMode = 0;
                bar->PartsTypeRenderType = 0;
                bar->PartID = 2;
                bar->AtkResNode.MultiplyRed = 255;
                bar->AtkResNode.MultiplyGreen = 255;
                bar->AtkResNode.MultiplyBlue = 0;
                bar->AtkResNode.AddRed = ushort.MaxValue;
                bar->AtkResNode.AddGreen = ushort.MaxValue;
                bar->AtkResNode.AddBlue = ushort.MaxValue;
                bar->AtkResNode.Width = 0;
                bar->AtkResNode.Flags_2 |= 1;
                bar->AtkResNode.SetHeight(20);
                bar->AtkResNode.SetWidth(160);
                bar->AtkResNode.SetScale(1, 1);
                bar->AtkResNode.ToggleVisibility(true);
                UiHelper.LinkNodeAfterTargetNode(&bar->AtkResNode, hpGaugeBar, &hpNineGrid->AtkResNode);
            }
            
            if (shieldGrid == null) {
                CreateBar(shieldGridID);
                return;
            }
            
            if (overShieldGrid == null) {
                CreateBar(overShieldGridID);
                return;
            }
            
            var player = Service.ClientState.LocalPlayer;
            if (player == null) return;

            var shieldRawPercentage = ((Character*) player.Address)->CharacterData.ShieldValue / 100f;
            var playerHpPercentage = (float)player.CurrentHp / player.MaxHp;
            var playerHpDownPercentage = 1f - playerHpPercentage;
            var shieldOverPercentage = shieldRawPercentage - playerHpDownPercentage;
            shieldOverPercentage = shieldOverPercentage < 0 ? 0 : shieldOverPercentage;
            var shieldPercentage = shieldRawPercentage - shieldOverPercentage;
            if (shieldOverPercentage > 1) shieldOverPercentage = 1;

            shieldGrid->AtkResNode.ToggleVisibility(shieldPercentage > 0);
            overShieldGrid->AtkResNode.ToggleVisibility(shieldOverPercentage > 0);
            
            shieldGrid->AtkResNode.X = playerHpPercentage * 148;
            shieldGrid->AtkResNode.SetWidth(shieldPercentage > 0 ? (ushort)(shieldPercentage * 148 + 12 + 0.5f) : (ushort)0);
            shieldGrid->AtkResNode.Flags_2 |= 1;
            overShieldGrid->AtkResNode.SetWidth(shieldOverPercentage > 0 ? (ushort)(shieldOverPercentage * 148 + 12 + 0.5f) : (ushort)0);

            shieldGrid->AtkResNode.MultiplyRed = (byte) (255 * Config.ShieldColour.X);
            shieldGrid->AtkResNode.MultiplyGreen = (byte) (255 * Config.ShieldColour.Y);
            shieldGrid->AtkResNode.MultiplyBlue = (byte) (255 * Config.ShieldColour.Z);

            overShieldGrid->AtkResNode.MultiplyRed = (byte) (255 * Config.ShieldColour.X);
            overShieldGrid->AtkResNode.MultiplyGreen = (byte) (255 * Config.ShieldColour.Y);
            overShieldGrid->AtkResNode.MultiplyBlue = (byte) (255 * Config.ShieldColour.Z);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }
}