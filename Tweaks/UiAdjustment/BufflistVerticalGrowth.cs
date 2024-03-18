using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Events;
using Dalamud.Logging;

using System.Collections.Generic;
using System.Linq;
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

        [TweakConfigOption("Justify Conditional Buff List to the bottom")]
        public bool ConditionalBuffListBool = false;

    }

    public Configs Config { get; private set; }



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
        UpdateHorizontalGrowth(3);
    }

    private unsafe void UpdateHorizontalGrowth(int index, bool getConfigBool = true)
    {
        string AddonName;
        bool ConfigBool;

        // max number of buffs show
        int maxNumberOfBuffs;
        // number of nodes before the first buff node
        int nodesBeforeBuffsList;

        switch (index)
        {
            case 0:
                AddonName = "_StatusCustom0";
                ConfigBool = Config.buffListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 1:
                AddonName = "_StatusCustom1";
                ConfigBool = Config.debuffListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 2:
                AddonName = "_StatusCustom2";
                ConfigBool = Config.aurasListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 3:
                AddonName = "_StatusCustom3";
                ConfigBool = Config.ConditionalBuffListBool;
                nodesBeforeBuffsList = 4;
                maxNumberOfBuffs = 8;
                break;
            default:
                PluginLog.Log($"Wrong index value, should be 0, 1, 2 or 3, got {index}");
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
        //if (targetStatusCustom->UldManager.NodeList == null || targetStatusCustom->UldManager.NodeListCount < 25) return;

        AtkResNode* node;



        // Get a list of a all icons Y positions
        List<float> yValuesArray = new List<float>();

        for (int i = 0; i < maxNumberOfBuffs; i++)
        {
            node = targetStatusCustom->UldManager.NodeList[maxNumberOfBuffs + nodesBeforeBuffsList -1 - i];

            yValuesArray.Add(node->Y);
        }

  

        IEnumerable<float> uniqueFloats = yValuesArray.Distinct();


        // fix for the 3x3 with 8 buffs and 1 vacant spot
        // in the Conditional Buff List
        // this assures the third and the sixth buff icon 
        // are in the first and second line respectively
        if (index == 3) 
        { 
            List<float> uniqueFloatsList = uniqueFloats.ToList();
            if (uniqueFloatsList.Count == 3)
            {
                yValuesArray[2] = yValuesArray[0];
                yValuesArray[5] = yValuesArray[3];
            }
        }

        float yValue;

        bool arraySorted = yValuesArray[0] < yValuesArray[maxNumberOfBuffs-1];

        for (var i = 0; i < maxNumberOfBuffs; i++)
        {
            // always get the smallest Y position first
            if (arraySorted)
            {
                yValue = yValuesArray[i];
            }
            else
            {
                yValue = yValuesArray[maxNumberOfBuffs-1 - i];
            }

            // if true start with the last icon (it has the smallest index)
            // else start with the first icon (it has the biggest index)
            if (ConfigBool)
            {
                node = targetStatusCustom->UldManager.NodeList[nodesBeforeBuffsList + i];
            }
            else
            {
                node = targetStatusCustom->UldManager.NodeList[maxNumberOfBuffs + nodesBeforeBuffsList - 1 - i];
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

    [AddonPostRequestedUpdate("_StatusCustom3")]
    private void AfterConditionalBuffListUpdate()
    {
        try
        {
            UpdateHorizontalGrowth(3);
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
        UpdateHorizontalGrowth(3, false);
        SaveConfig(Config);
        base.Disable();
    }
}