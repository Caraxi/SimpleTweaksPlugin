using System;
using System.Numerics;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ShowShieldOnHPBar: UiAdjustments.SubTweak
{
    public override string Name => "Shield on HP Bar";
    public override string Description => "Show approximate shield on the HP Bar.";

    protected override string Author => "Chivalrik";

    public class Configs : TweakConfig {
        [TweakConfigOption("Shield Colour", "Color")]
        public Vector3 ShieldColour = Common.UiColorToVector3(0xFFFF00);
    }

    public Configs Config { get; private set; }
    public override bool UseAutoConfig => true;

    private AtkNineGridNode* shieldGrid;
    private AtkNineGridNode* shieldGridOver;
    public override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += FrameworkOnUpdateSetup;
        base.Enable();
    }
        
    public override void Disable()
    {
        SaveConfig(Config);
        Service.Framework.Update -= FrameworkOnUpdateSetup;
        Service.Framework.Update -= FrameworkOnUpdate;
        if (shieldGrid != null)
        {
            shieldGrid->AtkResNode.Width = 0;
        }
        if (shieldGridOver != null)
        {
            shieldGridOver->AtkResNode.Width = 0;
        }
        base.Disable();
    }

    private void FrameworkOnUpdateSetup(Framework framework)
    {           
        try
        {
            var parameterWidget = Common.GetUnitBase("_ParameterWidget");
            if (parameterWidget == null) return;
            if (parameterWidget->UldManager.LoadedState != AtkLoadState.Loaded) return;
            if (!parameterWidget->IsVisible) return;
            var hpGaugeBar = ((AtkComponentNode*) parameterWidget->UldManager.NodeList[2]);
            var hpNineGrid = (AtkNineGridNode*) hpGaugeBar->Component->UldManager.NodeList[3];
            if (hpGaugeBar->Component->UldManager.NodeListCount == 7)
            {
                // Create Nodes
                UiHelper.ExpandNodeList(hpGaugeBar, 2);

                shieldGrid = UiHelper.CloneNode(hpNineGrid);
                //shieldGrid->AtkResNode.NodeID = NodeSlideCastMarker;
                hpGaugeBar->Component->UldManager.NodeList[6]->PrevSiblingNode = (AtkResNode*) shieldGrid;
                shieldGrid->AtkResNode.NextSiblingNode = hpGaugeBar->Component->UldManager.NodeList[6];
                shieldGrid->AtkResNode.ParentNode = (AtkResNode*) hpGaugeBar;
                hpGaugeBar->Component->UldManager.NodeList[hpGaugeBar->Component->UldManager.NodeListCount++] =
                    (AtkResNode*) shieldGrid;
                shieldGrid->AtkResNode.MultiplyRed = 255;
                shieldGrid->AtkResNode.MultiplyGreen = 255;
                shieldGrid->AtkResNode.MultiplyBlue = 0;
                shieldGrid->AtkResNode.AddRed = ushort.MaxValue;
                shieldGrid->AtkResNode.AddGreen = ushort.MaxValue;
                shieldGrid->AtkResNode.AddBlue = ushort.MaxValue;
                shieldGrid->PartID = 5;
                shieldGrid->AtkResNode.Width = 0;
                shieldGrid->AtkResNode.Flags_2 |= 1;

                shieldGridOver = UiHelper.CloneNode(hpNineGrid);
                shieldGrid->AtkResNode.PrevSiblingNode = (AtkResNode*) shieldGridOver;
                shieldGridOver->AtkResNode.NextSiblingNode = (AtkResNode*) shieldGrid;
                shieldGridOver->AtkResNode.ParentNode = (AtkResNode*) hpGaugeBar;
                hpGaugeBar->Component->UldManager.NodeList[hpGaugeBar->Component->UldManager.NodeListCount++] =
                    (AtkResNode*) shieldGridOver;
                shieldGridOver->AtkResNode.MultiplyRed = 255;
                shieldGridOver->AtkResNode.MultiplyGreen = 255;
                shieldGridOver->AtkResNode.MultiplyBlue = 0;
                shieldGridOver->AtkResNode.AddRed = ushort.MaxValue;
                shieldGridOver->AtkResNode.AddGreen = ushort.MaxValue;
                shieldGridOver->AtkResNode.AddBlue = ushort.MaxValue;
                shieldGridOver->PartID = 5;
                shieldGridOver->AtkResNode.Width = 0;
                shieldGridOver->AtkResNode.Flags_2 |= 1;
            }

            shieldGrid = (AtkNineGridNode*) hpGaugeBar->Component->UldManager.NodeList[7];
            shieldGridOver = (AtkNineGridNode*) hpGaugeBar->Component->UldManager.NodeList[8];
            Service.Framework.Update -= FrameworkOnUpdateSetup;
            Service.Framework.Update += FrameworkOnUpdate;
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    } 
        
    private void FrameworkOnUpdate(Framework framework)
    {
        try
        {
            var player = Service.ClientState.LocalPlayer;
            if (player == null) return;
            // Shield Percentage as a byte is at address 0x1997
            var shieldRawPercentage = ((Character*) player.Address)->ShieldValue / 100f;
            var playerHpPercentage = (float)player.CurrentHp / player.MaxHp;
            var playerHpDownPercentage = 1f - playerHpPercentage;
            var shieldOverPercentage = shieldRawPercentage - playerHpDownPercentage;
            shieldOverPercentage = shieldOverPercentage < 0 ? 0 : shieldOverPercentage;
            var shieldPercentage = shieldRawPercentage - shieldOverPercentage;
            if (shieldOverPercentage > 1) shieldOverPercentage = 1;

            shieldGrid->AtkResNode.X = playerHpPercentage * 148;
            shieldGrid->AtkResNode.Width = shieldPercentage > 0 ? (ushort)(shieldPercentage * 148 + 12 + 0.5f) : (ushort)0;
            shieldGrid->AtkResNode.Flags_2 |= 1;
            shieldGridOver->AtkResNode.Width = shieldOverPercentage > 0 ? (ushort)(shieldOverPercentage * 148 + 12 + 0.5f) : (ushort)0;

            shieldGrid->AtkResNode.MultiplyRed = (byte) (255 * Config.ShieldColour.X);
            shieldGrid->AtkResNode.MultiplyGreen = (byte) (255 * Config.ShieldColour.Y);
            shieldGrid->AtkResNode.MultiplyBlue = (byte) (255 * Config.ShieldColour.Z);

            shieldGridOver->AtkResNode.MultiplyRed = (byte) (255 * Config.ShieldColour.X);
            shieldGridOver->AtkResNode.MultiplyGreen = (byte) (255 * Config.ShieldColour.Y);
            shieldGridOver->AtkResNode.MultiplyBlue = (byte) (255 * Config.ShieldColour.Z);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }
    }
}