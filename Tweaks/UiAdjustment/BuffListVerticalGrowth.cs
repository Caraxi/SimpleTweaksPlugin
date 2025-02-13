using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Events;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Grow Buff List Vertically")]
[TweakDescription("Allows you to change the buff/debuff vertical justification, enabling the list to grow from bottom to top.")]
[TweakAuthor("LINKAD0")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.8.0")]
public unsafe class BuffListVerticalGrowth : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Buff List", HelpText = "Group 1: Status Info (Enhancements)")]
        public bool BuffListBool;

        [TweakConfigOption("Debuff List", HelpText = "Group 3: Status Info (Enfeeblements)")]
        public bool DebuffListBool;

        [TweakConfigOption("Food/FC Buffs List", HelpText = "Group 4: Status Info (Others)")]
        public bool AurasListBool;

        [TweakConfigOption("Conditional Buff List", HelpText = "Group 2: Status Info (Conditional Enhancements)")]
        public bool ConditionalBuffListBool;
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected override void Enable() {
        ConfigChanged();
    }

    protected override void ConfigChanged() {
        UpdateHorizontalGrowth(0);
        UpdateHorizontalGrowth(1);
        UpdateHorizontalGrowth(2);
        UpdateHorizontalGrowth(3);
    }

    private void UpdateHorizontalGrowth(int index, bool getConfigBool = true) {
        string addonName;
        bool configBool;

        // max number of buffs show
        int maxNumberOfBuffs;
        // number of nodes before the first buff node
        int nodesBeforeBuffsList;

        switch (index) {
            case 0:
                addonName = "_StatusCustom0";
                configBool = Config.BuffListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 1:
                addonName = "_StatusCustom1";
                configBool = Config.DebuffListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 2:
                addonName = "_StatusCustom2";
                configBool = Config.AurasListBool;
                nodesBeforeBuffsList = 5;
                maxNumberOfBuffs = 20;
                break;
            case 3:
                addonName = "_StatusCustom3";
                configBool = Config.ConditionalBuffListBool;
                nodesBeforeBuffsList = 4;
                maxNumberOfBuffs = 8;
                break;
            default:
                SimpleLog.Log($"Wrong index value, should be 0, 1, 2 or 3, got {index}");
                return;
        }

        if (!getConfigBool) {
            configBool = false;
        }

        var targetStatusCustom = (AtkUnitBase*)Service.GameGui.GetAddonByName(addonName);
        if (targetStatusCustom == null) return;
        //if (targetStatusCustom->UldManager.NodeList == null || targetStatusCustom->UldManager.NodeListCount < 25) return;

        AtkResNode* node;

        // Get a list of a all icons Y positions
        var yValuesArray = new List<float>();

        for (var i = 0; i < maxNumberOfBuffs; i++) {
            node = targetStatusCustom->UldManager.NodeList[maxNumberOfBuffs + nodesBeforeBuffsList - 1 - i];
            yValuesArray.Add(node->Y);
        }

        // fix for the 3x3 with 8 buffs and 1 vacant spot
        // in the Conditional Buff List
        // this assures the third and the sixth buff icon 
        // are in the first and second line respectively
        if (index == 3) {
            var uniqueFloats = yValuesArray.Distinct();
            var uniqueFloatsList = uniqueFloats.ToList();
            if (uniqueFloatsList.Count == 3) {
                yValuesArray[2] = yValuesArray[0];
                yValuesArray[5] = yValuesArray[3];
            }
        }

        var arraySorted = yValuesArray[0] < yValuesArray[maxNumberOfBuffs - 1];

        for (var i = 0; i < maxNumberOfBuffs; i++) {
            // always get the smallest Y position first
            var yValue = arraySorted ? yValuesArray[i] : yValuesArray[maxNumberOfBuffs - 1 - i];

            // if true start with the last icon (it has the smallest index)
            // else start with the first icon (it has the biggest index)
            node = configBool ? targetStatusCustom->UldManager.NodeList[nodesBeforeBuffsList + i] : targetStatusCustom->UldManager.NodeList[maxNumberOfBuffs + nodesBeforeBuffsList - 1 - i];
            node->Y = yValue;
            node->DrawFlags |= 0x1;
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom0")]
    private void AfterBuffListUpdate() {
        try {
            UpdateHorizontalGrowth(0);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom1")]
    private void AfterDebuffListUpdate() {
        try {
            UpdateHorizontalGrowth(1);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom2")]
    private void AfterAuraListUpdate() {
        try {
            UpdateHorizontalGrowth(2);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    [AddonPostRequestedUpdate("_StatusCustom3")]
    private void AfterConditionalBuffListUpdate() {
        try {
            UpdateHorizontalGrowth(3);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    protected override void Disable() {
        UpdateHorizontalGrowth(0, false);
        UpdateHorizontalGrowth(1, false);
        UpdateHorizontalGrowth(2, false);
        UpdateHorizontalGrowth(3, false);
    }
}
