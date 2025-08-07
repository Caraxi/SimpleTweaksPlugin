using System;
using Dalamud.Game.Config;
using Dalamud.Interface.Colors;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide Achievements Nearing Completion Notifications")]
[TweakDescription("Completely hides the login/zone-in notification for achievements nearing completion.")]
[TweakAuthor("Anna")]
[TweakAutoConfig]
public class HideAchievementsNotifications : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool HideLogIn = true;
        public bool HideZoneIn = true;
    }

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox(LocString("HideLogIn", "Hide the login notification."), ref Config.HideLogIn);

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

        hasChanged |= ImGui.Checkbox(LocString("HideZoneIn", "Hide the zone-in notification."), ref Config.HideZoneIn);
    }

    [FrameworkUpdate(NthTick = 10)]
    private void HideNotifications() {
        if (Config.HideLogIn) HideNotification("_NotificationAchieveLogIn");
        if (Config.HideZoneIn) HideNotification("_NotificationAchieveZoneIn");
    }

    private unsafe void HideNotification(string name) {
        try {
            var atkUnitBase = Common.GetUnitBase(name);
            if (atkUnitBase == null || atkUnitBase->IsVisible == false) return;
            atkUnitBase->Hide(false, false, 1);
        } catch (Exception) {
            // ignore
        }
    }
}
