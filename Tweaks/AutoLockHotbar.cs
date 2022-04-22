using Dalamud.Game.ClientState.Conditions;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class AutoLockHotbar : Tweak {
    public override string Name => "Auto Lock Action Bars";
    public override string Description => "Automatically locks action bars when certain conditions are met.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Lock at beginning of combat.")]
        public bool CombatStart = true;

        [TweakConfigOption("Lock when changing zone.")]
        public bool ZoneChange = true;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
        Service.Condition.ConditionChange += OnConditionChange;
        base.Enable();
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (Config.CombatStart && flag == ConditionFlag.InCombat && value) SetLock(true);
    }

    private void OnTerritoryChanged(object? sender, ushort _) {
        if (Config.ZoneChange) SetLock(true);
    }

    public override void Disable() {
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.Condition.ConditionChange -= OnConditionChange;
        SaveConfig(Config);
        base.Disable();
    }

    private void SetLock(bool lockHotbar) {
        var actionBar = Common.GetUnitBase("_ActionBar");
        if (actionBar == null) return;
        Common.GenerateCallback(actionBar, 8, 3, 51u, 0u, lockHotbar);
    }
}