using System;
using Dalamud.Game;
using Dalamud.Game.Config;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class HideAchievementsNotifications : UiAdjustments.SubTweak {
        public class Configs : TweakConfig {
            public bool HideLogIn = true;
            public bool HideZoneIn = true;
        }

        public Configs Config { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox(LocString("HideLogIn", "Hide the login notification."), ref this.Config.HideLogIn);
            
            if (Service.GameConfig.TryGet(UiConfigOption.AchievementAppealLoginDisp, out bool achievementLoginDisplay) && achievementLoginDisplay == false) {
                ImGui.Indent();
                ImGui.Indent();
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                
                ImGui.TextWrapped("The game option 'Display achievements nearing completion as login notification' should be enabled to completely hide the on-login achievement reccomendations. It is currently disabled.");
                ImGui.PopStyleColor();
                if (ImGui.Button("Enable It")) {
                    Service.GameConfig.Set(UiConfigOption.AchievementAppealLoginDisp, true);
                }
                ImGui.Unindent();
                ImGui.Unindent();
            }
            
            hasChanged |= ImGui.Checkbox(LocString("HideZoneIn", "Hide the zone-in notification."), ref this.Config.HideZoneIn);
        };

        public override string Name => "Hide Achievements Nearing Completion Notifications";
        public override string Description => "Completely hides the login/zone-in notification for achievements nearing completion.";
        protected override string Author => "Anna";

        protected override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            Service.Framework.Update += this.HideNotifications;
            base.Enable();
        }

        protected override void Disable() {
            SaveConfig(Config);
            Service.Framework.Update -= this.HideNotifications;
            base.Disable();
        }

        private void HideNotifications(Framework framework) {
            if (this.Config.HideLogIn) {
                this.HideNotification("_NotificationAchieveLogIn");
            }

            if (this.Config.HideZoneIn) {
                this.HideNotification("_NotificationAchieveZoneIn");
            }
        }

        private unsafe void HideNotification(string name) {
            try {
                var atkUnitBase = Common.GetUnitBase(name);
                if (atkUnitBase == null || atkUnitBase->IsVisible == false) return;
                // vfunc for hide is currently incorrect, this PR fixes it: https://github.com/aers/FFXIVClientStructs/pull/567/files 
                ((delegate*unmanaged<AtkUnitBase*, bool, bool, bool, void>)atkUnitBase->VTable->Hide)(atkUnitBase, false, false, true);
            } catch (Exception) {
                // ignore
            }
        }
    }
}
