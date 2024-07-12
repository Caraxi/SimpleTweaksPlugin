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

        [TweakConfigOption("Lock when changing zone.")]
        public bool ZoneChange = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
        Service.Condition.ConditionChange += OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (Config.CombatStart && flag == ConditionFlag.InCombat && value) SetLock(true);
    }

    [TerritoryChanged]
    private void OnTerritoryChanged(ushort _) {
        if (Config.ZoneChange) SetLock(true);
    }

    protected override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
    }

    private void SetLock(bool lockHotbar) {
        var actionBar = Common.GetUnitBase("_ActionBar");
        if (actionBar == null) return;
        Common.GenerateCallback(actionBar, 9, 3, 51u, 0u, lockHotbar);
    }
}
