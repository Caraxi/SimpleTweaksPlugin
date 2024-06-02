﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

#if DEBUG
using SimpleTweaksPlugin.Debugging;
#endif

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class HideJobGauge : UiAdjustments.SubTweak {
        public override string Name => "Hide Job Gauge";
        public override string Description => "Allow hiding the job gauge while not in combat or dungeons.";

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
        public override bool UseAutoConfig => true;
        private readonly Stopwatch outOfCombatTimer = new Stopwatch();
        
        public override void Setup() {
            base.Setup();
            AddChangelog("1.8.7.2", "Fixed 'Show while weapon is drawn' option not working.");
            AddChangelog("1.8.8.0", "Fixed 'Show In Duty' option not working in some duties.");
        }

        protected override void Enable() {
            outOfCombatTimer.Restart();
            Config = LoadConfig<Configs>() ?? new Configs();
            Common.FrameworkUpdate += FrameworkUpdate;
            base.Enable();
        }
        
        private void FrameworkUpdate() {
            try {
                Update();
            } catch {
                // 
            }
            
        }

        private readonly ushort[] nonCombatTerritory = {
            1055, // Island Sanctuary
        };

        private bool InCombatDuty => Service.Condition.Duty() && !nonCombatTerritory.Contains(Service.ClientState.TerritoryType);
        
        private void Update(bool reset = false) {
            if (Common.GetUnitBase("JobHudNotice") != null) reset = true;
            var stage = AtkStage.GetSingleton();
            var loadedUnitsList = &stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
#if DEBUG
            PerformanceMonitor.Begin();
            #endif
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
            #if DEBUG
            PerformanceMonitor.End();
            #endif
        }

        protected override void Disable() {
            Common.FrameworkUpdate -= FrameworkUpdate;
            try {
                Update(true);
            } catch {
                //
            }
            SaveConfig(Config);
            base.Disable();
        }
    }
}
