using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide Job Gauge")]
[TweakDescription("Allow hiding the job gauge while not in combat or dungeons.")]
[TweakAutoConfig]
[Changelog("1.8.7.2", "Fixed 'Show while weapon is drawn' option not working.")]
[Changelog("1.8.8.0", "Fixed 'Show In Duty' option not working in some duties.")]
public unsafe class HideJobGauge : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Show In Duty", 1)] 
        public bool ShowInDuty = true;

        [TweakConfigOption("Show In Combat", 2)]
        public bool ShowInCombat = true;

        public bool ShouldShowCombatBuffer() => ShowInCombat;

        [TweakConfigOption("Out of Combat Time (Seconds)", 3, EditorSize = 100, IntMin = 0, IntMax = 300, ConditionalDisplay = true)]
        public int CombatBuffer;

        [TweakConfigOption("Show While Weapon Is Drawn", 4)]
        public bool ShowWhileWeaponDrawn = true;
    }

    public Configs Config { get; private set; }

    private readonly Stopwatch outOfCombatTimer = new();

    protected override void Enable() => outOfCombatTimer.Restart();

    [FrameworkUpdate] private void FrameworkUpdate() => Update();

    private readonly ushort[] nonCombatTerritory = [1055];

    private bool InCombatDuty => Service.Condition.Duty() && !nonCombatTerritory.Contains(Service.ClientState.TerritoryType);

    private void Update(bool reset = false) {
        try {
            if (Common.GetUnitBase("JobHudNotice") != null) reset = true;
            var stage = AtkStage.Instance();
            var loadedUnitsList = &stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
            var character = (Character*)(Service.ClientState.LocalPlayer?.Address ?? nint.Zero);

            foreach (var j in Enumerable.Range(0, Math.Min(loadedUnitsList->Count, loadedUnitsList->Entries.Length))) {
                var addon = loadedUnitsList->Entries[j].Value;
                if (addon == null) continue;
                var name = addon->NameString;

                if (name != null && name.StartsWith("JobHud")) {
                    if (reset || Config.ShowInDuty && InCombatDuty) {
                        if (addon->UldManager.NodeListCount == 0) addon->UldManager.UpdateDrawNodeList();
                    } else if (Config.ShowInCombat && Service.Condition[ConditionFlag.InCombat]) {
                        outOfCombatTimer.Restart();
                        if (addon->UldManager.NodeListCount == 0) addon->UldManager.UpdateDrawNodeList();
                    } else if (Config.ShowInCombat && outOfCombatTimer.ElapsedMilliseconds < Config.CombatBuffer * 1000) {
                        if (addon->UldManager.NodeListCount == 0) addon->UldManager.UpdateDrawNodeList();
                    } else if (Config.ShowWhileWeaponDrawn && character != null && character->IsWeaponDrawn) {
                        if (addon->UldManager.NodeListCount == 0) addon->UldManager.UpdateDrawNodeList();
                    } else {
                        addon->UldManager.NodeListCount = 0;
                    }
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void Disable() => Update(true);
}
