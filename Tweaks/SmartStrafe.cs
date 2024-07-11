using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;


[TweakName("Smart Strafe")]
[TweakDescription("Intelligently switches keyboard controls between strafing and turning.\n(Legacy type movement only)")]
[TweakAuthor("Iryoku")]
[TweakAutoConfig]
internal class SmartStrafe : Tweak {
    public enum Mode {
        Turning,
        Strafing,
        StrafingNoBackpedal,
    }

    public class Configs : TweakConfig {
        public Mode InCombat = Mode.StrafingNoBackpedal;
        public Mode OutOfCombat = Mode.Turning;
        public bool ManualBackpedal = true;
    }

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool changed) {
        changed |= DrawModeSelector(LocString("InCombat", "In combat"), ref Config.InCombat);
        changed |= DrawModeSelector(LocString("OutOfCombat", "Out of combat"), ref Config.OutOfCombat);

        changed |= ImGui.Checkbox(
            LocString("ManualBackpedal", "Enable manual quick turning and backpedaling"),
            ref Config.ManualBackpedal);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(LocString("ManualBackpedalTooltip", "Activates strafing when both left and right are held"));
        }
    }

    private bool DrawModeSelector(string label, ref Mode selectedMode) {
        var changed = false;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo(label, GetModeLabel(selectedMode))) {
            foreach (var mode in Enum.GetValues<Mode>()) {
                if (ImGui.Selectable(GetModeLabel(mode), mode == selectedMode)) {
                    selectedMode = mode;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    private string GetModeLabel(Mode mode) {
        return LocString(mode.ToString(), mode switch {
            Mode.StrafingNoBackpedal => "Strafing (no backpedaling)",
            _ => mode.ToString(),
        });
    }

    private static class Signatures {
        internal const string CheckStrafeKeybind = "E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 06 01 BA 44 01 00 00";
    }

    private enum Keybind : int {
        MoveForward = 321,
        MoveBack = 322,
        TurnLeft = 323,
        TurnRight = 324,
        StrafeLeft = 325,
        StrafeRight = 326,
    }

    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool CheckStrafeKeybindDelegate(IntPtr ptr, Keybind keybind);

    private HookWrapper<CheckStrafeKeybindDelegate> Hook;

    protected override void Enable() {
        Hook ??= Common.Hook(
            Service.SigScanner.ScanText(Signatures.CheckStrafeKeybind), 
            new CheckStrafeKeybindDelegate(CheckStrafeKeybind));
        Hook.Enable();
    }

    protected override void Disable()
    {
        Hook.Disable();
    }

    private bool CheckStrafeKeybind(IntPtr ptr, Keybind keybind)
    {
        if (keybind is Keybind.StrafeLeft or Keybind.StrafeRight && !Service.ClientState.IsGPosing) {
            if (Config.ManualBackpedal &&
                (Hook.Original(ptr, Keybind.TurnLeft) || Hook.Original(ptr, Keybind.StrafeLeft)) &&
                (Hook.Original(ptr, Keybind.TurnRight) || Hook.Original(ptr, Keybind.StrafeRight))) {
                return true;
            }

            var mode = Service.Condition[ConditionFlag.InCombat]
                ? Config.InCombat
                : Config.OutOfCombat;

            switch (mode) {
                default: break;
                case Mode.Turning: return false;
                case Mode.StrafingNoBackpedal:
                    if (Hook.Original(ptr, Keybind.MoveBack)) {
                        return false;
                    }
                    goto case Mode.Strafing;
                case Mode.Strafing:
                    return Hook.Original(ptr, keybind - 2) || Hook.Original(ptr, keybind);
            }
        }

        return Hook.Original(ptr, keybind);
    }
}
