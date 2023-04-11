using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
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
        [TweakConfigOption("Hide Pop-up Help out of Combat", 5)]
        public bool HidePopUp = false;
        [TweakConfigOption("Hide Pop-up Help out of Combat", 6)]
        public bool HidePopUpOoc = false;
    }

    public bool OriginalAction;
    public bool OriginalItem;
    public bool OriginalPopUp;
    
    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() {
        OnConditionChange(ConditionFlag.InCombat, false);
        OnConditionChange(ConditionFlag.InCombat, Service.Condition[ConditionFlag.InCombat]);
        base.ConfigChanged();
    }

    public override void Setup() {
        AddChangelog("1.8.6.1", "Improved logic to attempt to reduce settings getting stuck in incorrect state.");
        base.Setup();
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Common.AddonSetup += OnAddonSetup;
        Service.ClientState.Login += OnLogin;
        if (Service.ClientState.LocalContentId != 0) OnLogin();
        if (Common.GetUnitBase("ConfigCharacterHudGeneral", out var addon)) ToggleConfigLock(addon, false);
        base.Enable();
    }

    private void OnLogin(object sender = null, EventArgs e = null) {
        OriginalAction = GameConfig.UiControl.GetBool("ActionDetailDisp");
        OriginalItem = GameConfig.UiControl.GetBool("ItemDetailDisp");
        OriginalPopUp = GameConfig.UiControl.GetBool("ToolTipDisp");
        Service.Condition.ConditionChange += OnConditionChange;
        OnConditionChange(ConditionFlag.InCombat, Service.Condition[ConditionFlag.InCombat]);
    }

    public override void Disable() {
        SaveConfig(Config);
        Service.Condition.ConditionChange -= OnConditionChange;
        if (Service.ClientState.LocalContentId != 0) OnConditionChange(ConditionFlag.InCombat, false);
        Common.AddonSetup -= OnAddonSetup;
        
        if (Common.GetUnitBase("ConfigCharacterHudGeneral", out var addon)) ToggleConfigLock(addon, true);
        
        base.Disable();
    }

    private void SetVisible(string name, bool visible) {
        if (GameConfig.UiControl.TryGetBool(name, out var isVisible)) {
            if (isVisible != visible) {
                GameConfig.UiControl.Set(name, visible);
            }
        }
    }
    
    private void OnAddonSetup(SetupAddonArgs obj) {
        if (obj.AddonName != "ConfigCharacterHudGeneral") return;
        ToggleConfigLock(obj.Addon, false);
    }

    private unsafe void ToggleConfigLock(AtkUnitBase* addon, bool allowEditing) {
        if (addon == null) return;

        void HandleCheckboxNode(uint id, uint addonTextId) {
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
                seString.Append(new UIForegroundPayload(500));
                seString.Append(" (Managed by Simple Tweaks)");
                seString.Append(new UIForegroundPayload(0));
            }
            textNode->NodeText.SetString(seString.Encode());
        }

        HandleCheckboxNode(30, 7663);
        HandleCheckboxNode(43, 7665);
        HandleCheckboxNode(47, 7666);
    }

    public void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag == ConditionFlag.LoggingOut && value) {
            SetVisible("ActionDetailDisp", OriginalAction);
            SetVisible("ItemDetailDisp", OriginalItem);
            SetVisible("ToolTipDisp", OriginalPopUp);
            return;
        }
        if (flag != ConditionFlag.InCombat) return;
        
        if (value) {
            SetVisible("ActionDetailDisp", !Config.HideAction);
            SetVisible("ItemDetailDisp", !Config.HideItem);
            SetVisible("ToolTipDisp", !Config.HidePopUp);
        } else {
            SetVisible("ActionDetailDisp", !Config.HideActionOoc);
            SetVisible("ItemDetailDisp", !Config.HideItemOoc);
            SetVisible("ToolTipDisp", !Config.HidePopUpOoc);
        }
    }
}
