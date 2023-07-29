using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class CombatMovementControl : Tweak {
    public override string Name => "Combat Movement Type Control";
    public override string Description => "Set movement type between Standard and Legacy when in/out of combat or when weapon is drawn/sheathed.";
    
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
        
        ShowOption("Out of Combat", ref Config.OutOfCombat);
        ShowOption("In Combat", ref Config.InCombat);
        ShowOption("Weapon Drawn", ref Config.WeaponDrawn);
        ShowOption("Weapon Sheathed", ref Config.WeaponSheathed);
    };

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Condition.ConditionChange += OnConditionChange;
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }


    private bool? previousUnsheathedState;
    private void OnFrameworkUpdate() {
        var unsheathedState = UIState.Instance()->WeaponState.IsUnsheathed;
        if (previousUnsheathedState == null) {
            previousUnsheathedState = unsheathedState;
            return;
        }
        
        if (unsheathedState != previousUnsheathedState) {
            previousUnsheathedState = unsheathedState;
            var v = unsheathedState ? Config.WeaponDrawn : Config.WeaponSheathed;
            if (v == MoveModeType.Ignore) return;
            Service.GameConfig.UiControl.Set("MoveMode", (uint) v);
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag == ConditionFlag.InCombat) {
            var v = value ? Config.InCombat : Config.OutOfCombat;
            if (v == MoveModeType.Ignore) return;
            Service.GameConfig.UiControl.Set("MoveMode", (uint) v);
        }
    }

    protected override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        SaveConfig(Config);
        base.Disable();
    }
}
