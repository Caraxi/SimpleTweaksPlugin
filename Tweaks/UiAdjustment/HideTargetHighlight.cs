using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide Target Hightlight/Outline")]
[TweakDescription("Allow hiding the potential target outline while not in combat or dungeons.")]
[TweakAutoConfig]
[TweakAuthor("origamitoaster")]
[TweakReleaseVersion("1.10.9.2")]
public unsafe class HideTargetHighlight : UiAdjustments.SubTweak {
    private readonly ushort[] nonCombatTerritory = {
        1055, // Island Sanctuary
    };

    private readonly Stopwatch outOfCombatTimer = new();
    public Configs Config { get; private set; }

    private bool InCombatDuty => Service.Condition[ConditionFlag.BoundByDuty] && !nonCombatTerritory.Contains(Service.ClientState.TerritoryType);

    protected override void Disable() => Update(true);
    protected override void Enable() => outOfCombatTimer.Restart();

    [FrameworkUpdate(NthTick = 30)] private void FrameworkUpdate() => Update();

    private void Update(bool reset = false) {
        var targetCircleShown = Service.GameConfig.UiControl.GetBool("ObjectBorderingType");
        bool requestToBeShown = false;
        bool requestToHide = false;

        if (reset || Config.ShowInDuty && InCombatDuty) {
            requestToBeShown = true;
        } else if (Config.ShowInCombat && Service.Condition[ConditionFlag.InCombat]) {
            requestToBeShown = true;
            outOfCombatTimer.Restart();
        } else if (Config.ShowInCombat && outOfCombatTimer.ElapsedMilliseconds < Config.CombatBuffer * 1000) {
            requestToBeShown = true;
        } else if (Config.ShowWhileWeaponDrawn && Service.ClientState.LocalPlayer != null && Service.ClientState.LocalPlayer.StatusFlags.HasFlag(StatusFlags.WeaponOut)) {
            requestToBeShown = true;
        } else {
            requestToHide = true;
        }

        if (!targetCircleShown && requestToBeShown) {
            Service.GameConfig.UiControl.Set("ObjectBorderingType", 1);
        } else if (targetCircleShown && requestToHide) {
            Service.GameConfig.UiControl.Set("ObjectBorderingType", 0);
        }
    }

    public class Configs : TweakConfig {
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
