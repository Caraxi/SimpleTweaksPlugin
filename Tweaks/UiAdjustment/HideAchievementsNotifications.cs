using System;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public HideAchievementsNotifications.Configs HideAchievementsNotifications = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class HideAchievementsNotifications : UiAdjustments.SubTweak {
        public class Configs {
            public bool HideLogIn = true;
            public bool HideZoneIn = true;
        }

        public Configs Config => this.PluginConfig.UiAdjustments.HideAchievementsNotifications;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Hide the login notification.", ref this.Config.HideLogIn);
            hasChanged |= ImGui.Checkbox("Hide the zone-in notification.", ref this.Config.HideZoneIn);
        };

        public override string Name => "Hide Achievements Nearing Completion Notifications";
        public override string Description => "Completely hides the login/zone-in notification for achievements nearing completion.";
        protected override string Author => "Anna";

        public override void Enable() {
            this.Plugin.PluginInterface.Framework.OnUpdateEvent += this.HideNotifications;
            base.Enable();
        }

        public override void Disable() {
            this.Plugin.PluginInterface.Framework.OnUpdateEvent -= this.HideNotifications;
            base.Disable();
        }

        private const int VisibilityFlag = 1 << 5;

        private void HideNotifications(Framework framework) {
            if (this.Config.HideLogIn) {
                this.HideNotification("_NotificationAchieveLogIn");
            }

            if (this.Config.HideZoneIn) {
                this.HideNotification("_NotificationAchieveZoneIn");
            }
        }

        private unsafe void HideNotification(string name) {
            var dalamudAddon = this.Plugin.PluginInterface.Framework.Gui.GetAddonByName(name, 1);
            if (dalamudAddon == null || dalamudAddon.Address == IntPtr.Zero) {
                return;
            }

            try {
                var atkUnitBase = (AtkUnitBase*) dalamudAddon.Address;
                atkUnitBase->Flags = (byte) (atkUnitBase->Flags & ~VisibilityFlag);
            } catch (Exception) {
                // ignore
            }
        }
    }
}
