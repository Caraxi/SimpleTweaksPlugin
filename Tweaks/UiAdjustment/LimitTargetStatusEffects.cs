using Dalamud.Game.Internal;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using System;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public LimitTargetStatusEffects.Configs LimitTargetStatusEffects = new LimitTargetStatusEffects.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class LimitTargetStatusEffects : UiAdjustments.SubTweak {
        public override string Name => "Limit Target Status Effects";
        public override string Description => "Sets a limit on the number of status effects displayed on your target.";
        protected override string Author => "Aireil";

        public class Configs {
            public int NbStatusEffects = 30;
            public bool LimitOnlyInCombat = false;
        }

        public Configs Config => PluginConfig.UiAdjustments.LimitTargetStatusEffects;
        private bool isDirty = false;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt("Number of status effects displayed##nbStatusEffectsDisplayed", ref Config.NbStatusEffects, 1);
            if (Config.NbStatusEffects < 0) Config.NbStatusEffects = 0;
            if (Config.NbStatusEffects > 30) Config.NbStatusEffects = 30;
            hasChanged |= ImGui.Checkbox("Only limit in combat##LimitOnlyInCombat", ref Config.LimitOnlyInCombat);

            UpdateTargetStatus(true);
        };

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            UpdateTargetStatus(true);
            base.Disable();
        }

        private void FrameworkOnUpdate(Framework framework) {
            try {
                UpdateTargetStatus();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateTargetStatus(bool reset = false) {
            var targetInfoUnitBase = Common.GetUnitBase("_TargetInfo", 1);
            if (targetInfoUnitBase == null) return;
            if (targetInfoUnitBase->UldManager.NodeList == null || targetInfoUnitBase->UldManager.NodeListCount < 53) return;

            var targetInfoStatusUnitBase = Common.GetUnitBase("_TargetInfoBuffDebuff", 1);
            if (targetInfoStatusUnitBase == null) return;
            if (targetInfoStatusUnitBase->UldManager.NodeList == null || targetInfoStatusUnitBase->UldManager.NodeListCount < 32) return;

            var isInCombat =
                this.PluginInterface.ClientState.Condition[Dalamud.Game.ClientState.ConditionFlag.InCombat];

            if (reset || (Config.LimitOnlyInCombat && !isInCombat && this.isDirty)) {
                for (var i = 32; i >= 3; i--) {
                    targetInfoUnitBase->UldManager.NodeList[i]->Color.A = 255;
                }

                for (var i = 31; i >= 2; i--) {
                    targetInfoStatusUnitBase->UldManager.NodeList[i]->Color.A = 255;
                }

                this.isDirty = false;

                return;
            }

            if (Config.LimitOnlyInCombat && !isInCombat) return;

            this.isDirty = true;

            for (var i = 32 - Config.NbStatusEffects; i >= 3; i--) {
                targetInfoUnitBase->UldManager.NodeList[i]->Color.A = 0;
            }

            for (var i = 31 - Config.NbStatusEffects; i >= 2; i--) {
                targetInfoStatusUnitBase->UldManager.NodeList[i]->Color.A = 0;
            }
        }
    }
}
