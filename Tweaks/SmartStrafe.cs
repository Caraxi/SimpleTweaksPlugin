using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using System;
using System.Runtime.InteropServices;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks
{
    [TweakName("Smart Strafe")]
    [TweakDescription("Intelligently switches keyboard controls between strafing and turning.\\n(Legacy type movement only)\"")]
    public class SmartStrafe : Tweak
    {
        public class Config : TweakConfig
        {
            public Mode InCombat = Mode.StrafingNoBackpedal;
            public Mode OutOfCombat = Mode.Turning;
            public bool ManualBackpedal = true;
        }

        public Config TweakConfig { get; private set; }

        public enum Mode
        {
            Turning,
            Strafing,
            StrafingNoBackpedal,
        }

        private enum Keybind : int
        {
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

        protected override void Enable()
        {
            TweakConfig = LoadConfig<Config>() ?? new Config();

            Hook ??= Common.Hook<CheckStrafeKeybindDelegate>(
                Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 3C B8 0D 00 00 00 66 89 07 C6 47 0C 03 89 6F 08 4D 85 F6"),
                new CheckStrafeKeybindDelegate(CheckStrafeKeybind));
            Hook.Enable();

            base.Enable();
        }

        protected override void Disable()
        {
            SaveConfig(TweakConfig);
            Hook.Disable();
            base.Disable();
        }

        public override void Dispose()
        {
            Hook.Dispose();
            base.Dispose();
        }

        private bool CheckStrafeKeybind(IntPtr ptr, Keybind keybind)
        {
            if (keybind is Keybind.StrafeLeft or Keybind.StrafeRight)
            {
                if (TweakConfig.ManualBackpedal &&
                    (Hook.Original(ptr, Keybind.TurnLeft) || Hook.Original(ptr, Keybind.StrafeLeft)) &&
                    (Hook.Original(ptr, Keybind.TurnRight) || Hook.Original(ptr, Keybind.StrafeRight)))
                {
                    return true;
                }

                var mode = Service.Condition[ConditionFlag.InCombat]
                    ? TweakConfig.InCombat
                    : TweakConfig.OutOfCombat;

                switch (mode)
                {
                    case Mode.Turning: return false;
                    case Mode.StrafingNoBackpedal:
                        if (Hook.Original(ptr, Keybind.MoveBack))
                        {
                            return false;
                        }
                        goto case Mode.Strafing;
                    case Mode.Strafing:
                        return Hook.Original(ptr, keybind - 2) || Hook.Original(ptr, keybind);
                }
            }

            return Hook.Original(ptr, keybind);
        }

        protected void DrawConfig(ref bool changed)
        {
            var inCombatMode = (int)TweakConfig.InCombat;
            var outOfCombatMode = (int)TweakConfig.OutOfCombat;
            var modeNames = Enum.GetNames(typeof(Mode));

            changed |= ImGui.Combo("In Combat", ref inCombatMode, modeNames, modeNames.Length);
            changed |= ImGui.Combo("Out of Combat", ref outOfCombatMode, modeNames, modeNames.Length);

            if (changed)
            {
                TweakConfig.InCombat = (Mode)inCombatMode;
                TweakConfig.OutOfCombat = (Mode)outOfCombatMode;
            }

            changed |= ImGui.Checkbox("Enable manual quick turning and backpedaling", ref TweakConfig.ManualBackpedal);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Activates strafing when both left and right are held");
            }
        }
    }
}