using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Events;
using Dalamud.Logging;

using System.Collections.Generic;
using System;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Buff List Vertical Growth")]
[TweakDescription("Allow to change the buff/debuff vertical justification, enabling the list to grow from bottom to top.")]
[TweakAuthor("LINKAD0")]
[TweakAutoConfig]

public unsafe class BufflistVerticalGrowth : UiAdjustments.SubTweak
{
    public class Configs : TweakConfig
    {
        [TweakConfigOption("Justify Buff List to the bottom")]
        public bool buffListBool = false;

        [TweakConfigOption("Justify Debuff List to the bottom")]
        public bool debuffListBool = false;

        [TweakConfigOption("Justify Auras List to the bottom")]
        public bool aurasListBool = false;
        
    }

    public Configs Config { get; private set; }

    // max number of buffs show
    private int MAX_NUMBER_OF_BUFFS = 20;
    // number of nodes before the first buff node
    private int NODES_BEFORE_BUFFS_LIST = 5;


    protected override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
        ConfigChanged();
        base.Enable();
    }

    protected override void ConfigChanged()
    {
        UpdateHorizontalGrowth(0);
        UpdateHorizontalGrowth(1);
        UpdateHorizontalGrowth(2);
    }

    private unsafe void UpdateHorizontalGrowth(int index, bool getConfigBool = true)
    {
        string AddonName;
        bool ConfigBool;
        switch (index)
        {
            case 0:
                AddonName = "_StatusCustom0";
                ConfigBool = Config.buffListBool;
                break;
            case 1:
                AddonName = "_StatusCustom1";
                ConfigBool = Config.debuffListBool;
                break;
            case 2:
                AddonName = "_StatusCustom2";
                ConfigBool = Config.aurasListBool;
                break;
            default:
                PluginLog.Log($"Wrong index value, should be 0, 1 or 2, got {index}");
                return;
        }
        if (getConfigBool)
        {}
        else
        {
            ConfigBool = false;
        }


        var targetStatusCustom = (AtkUnitBase*)Service.GameGui.GetAddonByName(AddonName ?? "", 1);
        if (targetStatusCustom == null) return;
        if (targetStatusCustom->UldManager.NodeList == null || targetStatusCustom->UldManager.NodeListCount < 25) return;

        AtkResNode* node;



        // Get a list of a all icons Y positions
        List<float> yValuesArray = new List<float>();

        for (int i = 0; i < MAX_NUMBER_OF_BUFFS; i++)
        {
            node = targetStatusCustom->UldManager.NodeList[MAX_NUMBER_OF_BUFFS + NODES_BEFORE_BUFFS_LIST -1 - i];

            yValuesArray.Add(node->Y);
        }


        float yValue;

        bool arraySorted = yValuesArray[0] < yValuesArray[MAX_NUMBER_OF_BUFFS-1];

        for (var i = 0; i < MAX_NUMBER_OF_BUFFS; i++)
        {
            // always get the smallest Y position first
            if (arraySorted)
            {
                yValue = yValuesArray[i];
            }
            else
            {
                yValue = yValuesArray[MAX_NUMBER_OF_BUFFS-1 - i];
            }

            // if true start with the last icon (it has the smallest index)
            // else start with the first icon (it has the biggest index)
            if (ConfigBool)
            {
                node = targetStatusCustom->UldManager.NodeList[NODES_BEFORE_BUFFS_LIST + i];
            }
            else
            {
                node = targetStatusCustom->UldManager.NodeList[MAX_NUMBER_OF_BUFFS + NODES_BEFORE_BUFFS_LIST - 1 - i];
            }

            node->Y = yValue;

            node->DrawFlags |= 0x1;

        }
    }

    [AddonPostRequestedUpdate("_StatusCustom0")]
    private void AfterBuffListUpdate()
    {
        try
        {
            UpdateHorizontalGrowth(0);
        }
        catch (Exception ex)
        {
            Plugin.Error(this, ex);
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom1")]
    private void AfterDebuffListUpdate()
    {
        try
        {
            UpdateHorizontalGrowth(1);
        }
        catch (Exception ex)
        {
            Plugin.Error(this, ex);
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom2")]
    private void AfterAuraListUpdate()
    {
        try
        {
            UpdateHorizontalGrowth(2);
        }
        catch (Exception ex)
        {
            Plugin.Error(this, ex);
        }
    }

    protected override void Disable()
    {
        UpdateHorizontalGrowth(0, false);
        UpdateHorizontalGrowth(1, false);
        UpdateHorizontalGrowth(2, false);
        SaveConfig(Config);
        base.Disable();
    }
}