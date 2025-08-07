using Dalamud.Game.ClientState.Conditions;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Auto Lock Action Bars")]
[TweakDescription("Automatically locks action bars when certain conditions are met.")]
[TweakAutoConfig]
public unsafe class AutoLockHotbar : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Lock at beginning of combat.")]
        public bool CombatStart = true;

        [TweakConfigOption("Unlock when combat ends.")]
        public bool CombatEnd = false;

        [TweakConfigOption("Lock when changing zone.")]
        public bool ZoneChange = true;
        
        [TweakConfigOption("Lock Cross Hotbar")]
        public bool CrossHotbar = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
        Service.Condition.ConditionChange += OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat) return;
        if (Config.CombatStart && value) SetLock(true);
        if (Config.CombatEnd && !value) SetLock(false);
    }

    [TerritoryChanged]
    private void OnTerritoryChanged(ushort _) {
        if (Config.ZoneChange) SetLock(true);
    }

    protected override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
    }

    private void SetLock(bool lockHotbar) {
        SetLockBar(lockHotbar);
        if (Config.CrossHotbar) SetLockCross(lockHotbar);
    }
    
    private void SetLockBar(bool lockHotbar) {
        var actionBar = Common.GetUnitBase("_ActionBar");
        if (actionBar == null) return;
        Common.GenerateCallback(actionBar, 10, 3, 51u, 0u, lockHotbar);
    }

    private void SetLockCross(bool lockHotbar) {
        var actionBar = Common.GetUnitBase("_ActionCross");
        if (actionBar == null) return;
        Common.GenerateCallback(actionBar, 10, 4, 62u, 10u, lockHotbar);
    }
}
