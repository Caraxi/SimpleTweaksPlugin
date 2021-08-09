using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;

#if DEBUG
using SimpleTweaksPlugin.Debugging;
#endif

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class HideJobGauge : UiAdjustments.SubTweak {
        public override string Name => "Hide Job Gauge";
        public override string Description => "Allow hiding the job gauge while not in combat or dungeons.";

        public class Configs : TweakConfig {

            [TweakConfigOption("Show In Duty", 1)]
            public bool ShowInDuty;

            [TweakConfigOption("Show In Combat", 2)]
            public bool ShowInCombat;

        }

        public Configs Config { get; private set; }
        public override bool UseAutoConfig => true;
        
        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            PluginLog.Log("What?");
            SimpleLog.Log("Enable");
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }
        
        private void FrameworkUpdate(Framework framework) {
            try {
                Update();
            } catch {
                // 
            }
            
        }

        private void Update(bool reset = false) {
            var stage = AtkStage.GetSingleton();
            var loadedUnitsList = &stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
            var addonList = &loadedUnitsList->AtkUnitEntries;
            #if DEBUG
            PerformanceMonitor.Begin();
            #endif
            for (var i = 0; i < loadedUnitsList->Count; i++) {
                var addon = addonList[i];
                var name = Marshal.PtrToStringAnsi(new IntPtr(addon->Name));
                
                if (name != null && name.StartsWith("JobHud")) {
                    if (reset || Config.ShowInDuty && PluginInterface.ClientState.Condition[ConditionFlag.BoundByDuty]) {
                        addon->Show(0);
                    } else if (Config.ShowInCombat && PluginInterface.ClientState.Condition[ConditionFlag.InCombat]) {
                        addon->Show(0);
                    } else {
                        addon->Hide(false);
                    }
                }

            }
            #if DEBUG
            PerformanceMonitor.End();
            #endif
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
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

