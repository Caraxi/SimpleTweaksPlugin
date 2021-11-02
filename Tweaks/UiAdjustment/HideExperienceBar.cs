using System;
using System.Linq;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class HideExperienceBar : UiAdjustments.SubTweak {
        public override string Name => "Hide Experience Bar at Max Level";
        public override string Description => "Hides the experience bar when at max level.";
        protected override string Author => "Anna";

        private uint MaxLevel { get; }

        public HideExperienceBar() {
            var oneAbove = Service.Data.Excel.GetSheet<ParamGrow>()!
                .Where(row => row.ExpToNext == 0)
                .Min(row => row.RowId);
            this.MaxLevel = oneAbove - 1;
        }

        public override void Enable() {
            Service.Framework.Update += this.UpdateFramework;
            base.Enable();
        }

        public override void Disable() {
            Service.Framework.Update -= this.UpdateFramework;
            try {
                SetExperienceBarVisible(true);
            } catch {
                // ignored
            }

            base.Disable();
        }

        private void UpdateFramework(Framework framework) {
            try {
                this.Update();
            } catch {
                // ignored
            }
        }

        private static unsafe void SetExperienceBarVisible(bool visible) {
            var expAddon = Service.GameGui.GetAddonByName("_Exp", 1);
            if (expAddon == IntPtr.Zero) {
                return;
            }

            var addon = (AtkUnitBase*) expAddon;
            addon->IsVisible = visible;
        }

        private void Update() {
            var player = Service.ClientState.LocalPlayer;
            if (player == null) {
                return;
            }

            var isLimited = player.ClassJob.GameData.IsLimitedJob;
            var visible = player.Level < (isLimited ? this.MaxLevel - 10 : this.MaxLevel);
            SetExperienceBarVisible(visible);
        }
    }
}
