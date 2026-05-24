using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Config;
using Dalamud.Interface.Colors;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

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

    [AddonPreShow("_NotificationAchieveLogIn", "_NotificationAchieveZoneIn")]
    private void HideNotification(AddonArgs args) {
        switch (args.AddonName) {
            case "_NotificationAchieveLogIn" when Config.HideLogIn:
            case "_NotificationAchieveZoneIn" when Config.HideZoneIn:
                args.PreventOriginal();
                break;
        }
    }
}
