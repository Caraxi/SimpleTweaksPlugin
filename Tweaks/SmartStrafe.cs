using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks
{
    internal class SmartStrafe : Tweak {
        public override string Name => "Smart Strafe";
        public override string Description => "Inteligently switches keyboard controls between strafing and turning.\n(Legacy type movmement only)";
        protected override string Author => "Iryoku";

        public enum Mode {
            Turning,
            Strafing,
            StrafingNoBackpedal,
        }

        public class Config : TweakConfig {
            public Mode InCombat = Mode.StrafingNoBackpedal;
            public Mode OutOfCombat = Mode.Turning;
            public bool ManualBackpedal = true;
        }

        public Config TweakConfig { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool changed) => {
            changed |= DrawModeSelector(LocString("InCombat", "In combat"), ref TweakConfig.InCombat);
            changed |= DrawModeSelector(LocString("OutOfCombat", "Out of combat"), ref TweakConfig.OutOfCombat);

            changed |= ImGui.Checkbox(
                LocString("ManualBackpedal", "Enable manual quick turning and backpedaling"),
                ref TweakConfig.ManualBackpedal);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(LocString("ManualBackpedalTooltip", "Activates strafing when both left and right are held"));
            }
        };

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
            internal const string CheckStrafeKeybind = "E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 07 01 BA 44 01 00 00";
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

        private Hook<CheckStrafeKeybindDelegate> Hook;

        public override void Enable() {
            TweakConfig = LoadConfig<Config>() ?? new Config();

            // There are about twenty different copies of this function that all
            // do almost exactly the same thing, but they are all called in
            // different places for different purposes. This one is used (among
            // other things) by the movement code to check if you are pressing the
            // strafe keybinds.
            Hook ??= new Hook<CheckStrafeKeybindDelegate>(
                Service.SigScanner.ScanText(Signatures.CheckStrafeKeybind),
                CheckStrafeKeybind);
            Hook.Enable();

            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(TweakConfig);

            Hook.Disable();

            base.Disable();
        }

        private bool CheckStrafeKeybind(IntPtr ptr, Keybind keybind)
        {
            if (keybind is Keybind.StrafeLeft or Keybind.StrafeRight) {
                if (TweakConfig.ManualBackpedal &&
                    (Hook.Original(ptr, Keybind.TurnLeft) || Hook.Original(ptr, Keybind.StrafeLeft)) &&
                    (Hook.Original(ptr, Keybind.TurnRight) || Hook.Original(ptr, Keybind.StrafeRight))) {
                    return true;
                }

                var mode = Service.Condition[ConditionFlag.InCombat]
                    ? TweakConfig.InCombat
                    : TweakConfig.OutOfCombat;

                switch (mode) {
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
}
