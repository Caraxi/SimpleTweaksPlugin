﻿using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class HideTooltipsInCombat : TooltipTweaks.SubTweak {
    public override string Name => "Hide Tooltips in Combat";
    public override string Description => "Allows hiding Action and/or Item tooltips while in combat.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Hide Action Tooltips in Combat", 1)]
        public bool HideAction = false;
        [TweakConfigOption("Hide Action Tooltips out of Combat", 2)]
        public bool HideActionOoc = false;
        [TweakConfigOption("Hide Item Tooltips in Combat", 3)]
        public bool HideItem = false;
        [TweakConfigOption("Hide Item Tooltips out of Combat", 4)]
        public bool HideItemOoc = false;
        [TweakConfigOption("Hide Pop-up Help in Combat", 5)]
        public bool HidePopUp = false;
        [TweakConfigOption("Hide Pop-up Help out of Combat", 6)]
        public bool HidePopUpOoc = false;
        [TweakConfigOption("Hide Cross Bar Hints in Combat", 7)]
        public bool HideCrossbarHints = false;
        [TweakConfigOption("Hide Cross Bar Hints out of Combat", 7)]
        public bool HideCrossbarHintsOoc = false;
    }

    public bool OriginalAction;
    public bool OriginalItem;
    public bool OriginalPopUp;
    public bool OriginalCrossbarHints;
    
    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() {
        OnConditionChange(ConditionFlag.InCombat, false);
        OnConditionChange(ConditionFlag.InCombat, Service.Condition[ConditionFlag.InCombat]);
        base.ConfigChanged();
    }

    public override void Setup() {
        AddChangelog("1.8.7.1", "Added support for crossbar hints.");
        AddChangelog("1.8.6.1", "Improved logic to attempt to reduce settings getting stuck in incorrect state.");
        base.Setup();
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.ClientState.Login += OnLogin;
        if (Service.ClientState.LocalContentId != 0) OnLogin();
        if (Common.GetUnitBase("ConfigCharacterHudGeneral", out var addon)) ToggleConfigLock(addon, false);
        if (Common.GetUnitBase("ConfigCharaHotbarXHB", out var xhbAddon)) ToggleXhbConfigLock(xhbAddon, false);
    }

    private void OnLogin() {
        OriginalAction = Service.GameConfig.UiControl.GetBool("ActionDetailDisp");
        OriginalItem = Service.GameConfig.UiControl.GetBool("ItemDetailDisp");
        OriginalPopUp = Service.GameConfig.UiControl.GetBool("ToolTipDisp");
        OriginalCrossbarHints = Service.GameConfig.UiConfig.GetBool("HotbarCrossHelpDisp");
        Service.Condition.ConditionChange += OnConditionChange;
        OnConditionChange(ConditionFlag.InCombat, Service.Condition[ConditionFlag.InCombat]);
    }

    protected override void Disable() {
        SaveConfig(Config);
        Service.Condition.ConditionChange -= OnConditionChange;
        if (Service.ClientState.LocalContentId != 0) OnConditionChange(ConditionFlag.InCombat, false);
        if (Common.GetUnitBase("ConfigCharacterHudGeneral", out var addon)) ToggleConfigLock(addon, true);
        if (Common.GetUnitBase("ConfigCharaHotbarXHB", out var xhbAddon)) ToggleXhbConfigLock(xhbAddon, true);
    }

    private void SetVisible(string name, bool visible, bool uiConfig = false) {
        if ((uiConfig ? Service.GameConfig.UiConfig : Service.GameConfig.UiControl).TryGetBool(name, out var isVisible)) {
            if (isVisible != visible) {
                (uiConfig ? Service.GameConfig.UiConfig : Service.GameConfig.UiControl).Set(name, visible);
            }
        }
    }
    
    [AddonPostSetup("ConfigCharacterHudGeneral", "ConfigCharaHotbarXHB")]
    private void OnAddonSetup(AddonSetupArgs obj) {
        switch (obj.AddonName) {
            case "ConfigCharacterHudGeneral":
                ToggleConfigLock((AtkUnitBase*)obj.Addon, false);
                break;
            case "ConfigCharaHotbarXHB":
                ToggleXhbConfigLock((AtkUnitBase*)obj.Addon, false);
                break;
        }
    }

    private void HandleCheckboxNode(AtkUnitBase* addon, bool allowEditing, uint id, uint addonTextId) {
        var checkboxNode = addon->GetNodeById(id);
        if (checkboxNode == null) return;
        var checkbox = checkboxNode->GetAsAtkComponentCheckBox();
        if (checkbox == null) return;
        checkbox->AtkComponentButton.AtkComponentBase.SetEnabledState(allowEditing);
            
        var textNode2 = checkbox->AtkComponentButton.AtkComponentBase.UldManager.SearchNodeById(2);
        if (textNode2 == null) return;
        var textNode = textNode2->GetAsAtkTextNode();
        if (textNode == null) return;

        var text = Service.Data.GetExcelSheet<Addon>()?.GetRow(addonTextId);
        if (text == null) return;

        var seString = text.Text.ToDalamudString();
        if (!allowEditing) {
            TooltipManager.AddTooltip(addon, &textNode->AtkResNode, $"Setting managed by Simple Tweak:\n  - {LocalizedName}");
            seString.Append(new UIForegroundPayload(500));
            seString.Append(" (Managed by Simple Tweaks)");
            seString.Append(new UIForegroundPayload(0));
        } else {
            TooltipManager.RemoveTooltip(addon, &textNode->AtkResNode);
        }
        textNode->NodeText.SetString(seString.Encode());
        textNode->ResizeNodeForCurrentText();
    }
    
    private unsafe void ToggleXhbConfigLock(AtkUnitBase* addon, bool allowEditing) {
        if (addon == null) return;
        HandleCheckboxNode(addon, allowEditing, 11, 7791);
    }

    private unsafe void ToggleConfigLock(AtkUnitBase* addon, bool allowEditing) {
        if (addon == null) return;
        HandleCheckboxNode(addon, allowEditing, 30, 7663);
        HandleCheckboxNode(addon, allowEditing, 43, 7665);
        HandleCheckboxNode(addon, allowEditing, 47, 7666);
    }

    public void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag == ConditionFlag.LoggingOut && value) {
            SetVisible("ActionDetailDisp", OriginalAction);
            SetVisible("ItemDetailDisp", OriginalItem);
            SetVisible("ToolTipDisp", OriginalPopUp);
            SetVisible("HotbarCrossHelpDisp", OriginalCrossbarHints, true);
            return;
        }
        if (flag != ConditionFlag.InCombat) return;
        
        if (value) {
            SetVisible("ActionDetailDisp", !Config.HideAction);
            SetVisible("ItemDetailDisp", !Config.HideItem);
            SetVisible("ToolTipDisp", !Config.HidePopUp);
            SetVisible("HotbarCrossHelpDisp", !Config.HideCrossbarHints, true);
        } else {
            SetVisible("ActionDetailDisp", !Config.HideActionOoc);
            SetVisible("ItemDetailDisp", !Config.HideItemOoc);
            SetVisible("ToolTipDisp", !Config.HidePopUpOoc);
            SetVisible("HotbarCrossHelpDisp", !Config.HideCrossbarHintsOoc, true);
        }
    }
}
