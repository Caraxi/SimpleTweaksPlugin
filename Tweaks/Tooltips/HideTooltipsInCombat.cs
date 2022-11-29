using Dalamud.Game.ClientState.Conditions;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public class HideTooltipsInCombat : TooltipTweaks.SubTweak {
    public override string Name => "Hide Tooltips in Combat";
    public override string Description => "Allows hiding Action and/or Item tooltips while in combat.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Hide Action Tooltips")]
        public bool HideAction;

        [TweakConfigOption("Hide Item Tooltips")]
        public bool HideItem;

        [TweakConfigOption("Hide Pop-up Help")]
        public bool HidePopUp;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() {
        OnConditionChange(ConditionFlag.InCombat, false);
        if (Service.Condition[ConditionFlag.InCombat]) OnConditionChange(ConditionFlag.InCombat, true);
        base.ConfigChanged();
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Condition.ConditionChange += OnConditionChange;
        if (Service.Condition[ConditionFlag.InCombat]) OnConditionChange(ConditionFlag.InCombat, true);
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        Service.Condition.ConditionChange -= OnConditionChange;
        OnConditionChange(ConditionFlag.InCombat, false);
        base.Disable();
    }

    private bool hidAction;
    private bool hidItem;
    private bool hidPopUp;
    
    public void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag != ConditionFlag.InCombat) return;
        
        if (value) {
            void Hide(string name, bool doHide, ref bool didHide) {
                if (doHide && GameConfig.UiControl.TryGetBool(name, out var isVisible) && isVisible) {
                    didHide = true;
                    GameConfig.UiControl.Set(name, false);
                }
            }
            
            Hide("ActionDetailDisp", Config.HideAction, ref hidAction);
            Hide("ItemDetailDisp", Config.HideItem, ref hidItem);
            Hide("ToolTipDisp", Config.HidePopUp, ref hidPopUp);
        } else {
            void Show(string name, bool doShow, ref bool isHide) {
                if (doShow && isHide) GameConfig.UiControl.Set(name, true);
                isHide = false;
            }
            
            Show("ActionDetailDisp", Config.HideAction, ref hidAction);
            Show("ItemDetailDisp", Config.HideItem, ref hidItem);
            Show("ToolTipDisp", Config.HidePopUp, ref hidPopUp);
        }
    }
}
