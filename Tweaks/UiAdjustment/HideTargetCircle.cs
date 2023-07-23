using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

#if DEBUG

using SimpleTweaksPlugin.Debugging;

#endif

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public unsafe class HideTargetCircle : UiAdjustments.SubTweak
    {
        private readonly ushort[] nonCombatTerritory = {
            1055, // Island Sanctuary
        };

        private readonly Stopwatch outOfCombatTimer = new Stopwatch();
        public Configs Config { get; private set; }
        public override string Description => "Allow hiding the target circle while not in combat or dungeons.";
        public override string Name => "Hide Target Circle";
        public override bool UseAutoConfig => true;
        protected override string Author => "darkarchon";

        private bool InCombatDuty => Service.Condition[ConditionFlag.BoundByDuty] && !nonCombatTerritory.Contains(Service.ClientState.TerritoryType);

        public override void Disable()
        {
            Service.Framework.Update -= FrameworkUpdate;
            try
            {
                Update(true);
            }
            catch
            {
                //
            }
            SaveConfig(Config);
            base.Disable();
        }

        public override void Enable()
        {
            outOfCombatTimer.Restart();
            Config = LoadConfig<Configs>() ?? new Configs();
            Service.Framework.Update += FrameworkUpdate;
            base.Enable();
        }

        private void FrameworkUpdate(Framework framework)
        {
            try
            {
                Update();
            }
            catch
            {
                //
            }
        }

        public override void Setup() {
            base.Setup();
            AddChangelogNewTweak("1.8.7.0");
        }

        private void Update(bool reset = false)
        {
#if DEBUG
            PerformanceMonitor.Begin();
#endif

            var targetCircleShown = Service.GameConfig.UiControl.GetBool("TargetCircleType");
            bool requestToBeShown = false;
            bool requestToHide = false;

            if (reset || Config.ShowInDuty && InCombatDuty)
            {
                requestToBeShown = true;
            }
            else if (Config.ShowInCombat && Service.Condition[ConditionFlag.InCombat])
            {
                requestToBeShown = true;
                outOfCombatTimer.Restart();
            }
            else if (Config.ShowInCombat && outOfCombatTimer.ElapsedMilliseconds < Config.CombatBuffer * 1000)
            {
                requestToBeShown = true;
            }
            else if (Config.ShowWhileWeaponDrawn && Service.ClientState.LocalPlayer != null && Service.ClientState.LocalPlayer.StatusFlags.HasFlag(StatusFlags.WeaponOut))
            {
                requestToBeShown = true;
            }
            else
            {
                requestToHide = true;
            }

            if (!targetCircleShown && requestToBeShown)
            {
                Service.GameConfig.UiControl.Set("TargetCircleType", 1);
            }
            else if (targetCircleShown && requestToHide)
            {
                Service.GameConfig.UiControl.Set("TargetCircleType", 0);
            }
#if DEBUG
            PerformanceMonitor.End();
#endif
        }

        public class Configs : TweakConfig
        {
            [TweakConfigOption("Out of Combat Time (Seconds)", 3, EditorSize = 100, IntMin = 0, IntMax = 300, ConditionalDisplay = true)]
            public int CombatBuffer = 3;

            [TweakConfigOption("Show In Combat", 2)]
            public bool ShowInCombat = true;

            [TweakConfigOption("Show In Duty", 1)]
            public bool ShowInDuty = true;

            [TweakConfigOption("Show While Weapon Is Drawn", 4)]
            public bool ShowWhileWeaponDrawn;

            public bool ShouldShowCombatBuffer() => ShowInCombat;
        }
    }
}