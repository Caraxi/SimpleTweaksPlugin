using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class CombatCrossbarHintControl : Tweak {
    public override string Name => "Combat Crossbar Hint Control";
    public override string Description => "Set crossbar hints on or off when in/out of combat or when weapon is drawn/sheathed.";
    
    public enum HintModeType {
        Ignore = -1,
        HideHints,
        ShowHints,
    }

    public static string HintModeName(HintModeType hmt) {
        return hmt switch {
            HintModeType.Ignore => "Ignore",
            HintModeType.HideHints => "Hide Hints",
            HintModeType.ShowHints => "Show Hints",
            _ => throw new ArgumentOutOfRangeException(nameof(hmt), hmt, null)
        };
    }

    public class Configs : TweakConfig {
        public HintModeType InCombat = HintModeType.Ignore;
        public HintModeType OutOfCombat = HintModeType.Ignore;
        public HintModeType WeaponDrawn = HintModeType.Ignore;
        public HintModeType WeaponSheathed = HintModeType.Ignore;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        void ShowOption(string label, ref HintModeType c) {
            if (ImGui.BeginCombo(label, $"{HintModeName(c)}")) {
                if (ImGui.Selectable(HintModeName(HintModeType.Ignore), c == HintModeType.Ignore)) c = HintModeType.Ignore;
                if (ImGui.Selectable(HintModeName(HintModeType.HideHints), c == HintModeType.HideHints)) c = HintModeType.HideHints;
                if (ImGui.Selectable(HintModeName(HintModeType.ShowHints), c == HintModeType.ShowHints)) c = HintModeType.ShowHints;
                ImGui.EndCombo();
            }
        }
        
        ShowOption("Out of Combat", ref Config.OutOfCombat);
        ShowOption("In Combat", ref Config.InCombat);
        ShowOption("Weapon Drawn", ref Config.WeaponDrawn);
        ShowOption("Weapon Sheathed", ref Config.WeaponSheathed);
    };
    
    public override void Enable() {
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
            if (v == HintModeType.Ignore) return;
            GameConfig.UiConfig.Set("HotbarCrossHelpDisp", (uint) v);
        }
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag == ConditionFlag.InCombat) {
            var v = value ? Config.InCombat : Config.OutOfCombat;
            if (v == HintModeType.Ignore) return;
            GameConfig.UiConfig.Set("HotbarCrossHelpDisp", (uint) v);
        }
    }

    public override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        SaveConfig(Config);
        base.Disable();
    }
}
