using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class CombatMovementControl : Tweak {
    
    private readonly ConfigModule* configModule = ConfigModule.Instance();
    private bool inDuty;
    private bool inCombat;

    public override string Name => "Combat Movement Type Control";
    public override string Description => "Set movement type between Standard and Legacy when in/out of duty, combat or when weapon is drawn/sheathed. Is Duty configured and you are in Duty, Combat and Weapon will be ignored. Is Combat configuerd and you are in Combat, Weapon will be ignored. ";
    
    public enum MoveModeType {
        Ignore = -1,
        Standard,
        Legacy,
    }
    
    public class Configs : TweakConfig {
        public MoveModeType InCombat = MoveModeType.Ignore;
        public MoveModeType OutOfCombat = MoveModeType.Ignore;
        public MoveModeType WeaponDrawn = MoveModeType.Ignore;
        public MoveModeType WeaponSheathed = MoveModeType.Ignore;
        public MoveModeType InDuty = MoveModeType.Ignore;
        public MoveModeType OutOfDuty = MoveModeType.Ignore;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        void ShowOption(string label, ref MoveModeType c) {
            if (ImGui.BeginCombo(label, $"{c}")) {
                if (ImGui.Selectable(nameof(MoveModeType.Ignore), c == MoveModeType.Ignore)) c = MoveModeType.Ignore;
                if (ImGui.Selectable(nameof(MoveModeType.Standard), c == MoveModeType.Standard)) c = MoveModeType.Standard;
                if (ImGui.Selectable(nameof(MoveModeType.Legacy), c == MoveModeType.Standard)) c = MoveModeType.Legacy;
                ImGui.EndCombo();
            }
        }

        ShowOption("In Duty", ref Config.InDuty);
        ShowOption("Out of Duty", ref Config.OutOfDuty);
        ShowOption("In Combat", ref Config.InCombat);
        ShowOption("Out of Combat", ref Config.OutOfCombat);
        ShowOption("Weapon Drawn", ref Config.WeaponDrawn);
        ShowOption("Weapon Sheathed", ref Config.WeaponSheathed);
    };
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Condition.ConditionChange += OnConditionChange;
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }


    private byte? previousUnsheathedState;
    private void OnFrameworkUpdate() {
        var unsheathedState = UIState.Instance()->WeaponState.WeaponUnsheathed;
        if (previousUnsheathedState == null) {
            previousUnsheathedState = unsheathedState;
            return;
        }
        
        if (unsheathedState != previousUnsheathedState && !inDuty && !inCombat) {
            previousUnsheathedState = unsheathedState;
            var v = unsheathedState == 1 ? Config.WeaponDrawn : Config.WeaponSheathed;
            if (v == MoveModeType.Ignore) return;

            ////GameConfig.UiControl.Set("MoveMode", (uint) v);

            var cVal = configModule->GetIntValue(ConfigOption.MoveMode);
            configModule->SetOption(ConfigOption.MoveMode, (int)v);
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {

        if (flag == ConditionFlag.BoundByDuty)
        {
            var v = value ? Config.InDuty : Config.OutOfDuty;
            if (v == MoveModeType.Ignore) return;

            ////GameConfig.UiControl.Set("MoveMode", (uint) v);

            var cVal = configModule->GetIntValue(ConfigOption.MoveMode);
            configModule->SetOption(ConfigOption.MoveMode, (int)v);
            inDuty = value;
        }

        if (flag == ConditionFlag.InCombat && !inDuty)
        {
            var v = value ? Config.InCombat : Config.OutOfCombat;
            if (v == MoveModeType.Ignore) return;

            ////GameConfig.UiControl.Set("MoveMode", (uint) v);

            var cVal = configModule->GetIntValue(ConfigOption.MoveMode);
            configModule->SetOption(ConfigOption.MoveMode, (int)v);
            inCombat = value;
        }
    }

    public override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        SaveConfig(Config);
        base.Disable();
    }
}
