using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace SimpleTweaksPlugin; 

public class ConfigWindow : SimpleWindow {
    public ConfigWindow() : base("Simple Tweaks Config") {
        Size = new Vector2(600, 400);
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(600, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        base.Draw();

        var config = SimpleTweaksPlugin.Plugin.PluginConfig;

        if (config.AnalyticsOptOut == false && config.MetricsIdentifier?.Length != 64) {
            ImGui.SetWindowFontScale(1.25f);
            ImGui.Text("Simple Tweaks Statistics Collection");
            ImGui.SetWindowFontScale(1f);
            ImGui.Separator();
            
            
            ImGui.TextWrapped("" +
                              "Simple tweaks now collects statistics of how many people have each tweak enabled. " +
                              "This allows me (Caraxi) to get a general idea of which tweaks are actually being used and give some kind of priority to adding additional features with the same ideas. " +
                              "By allowing collection an anonymous list of your enabled tweaks will be collected and stored on my server. \n\n" + 
                              "You may opt out of this collection now and no information will be sent. You may also choose to opt back in at a later date in the simple tweaks config.");

            
            ImGui.Dummy(new Vector2(20));

            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.3f, 0.8f))) 
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.7f, 0.3f, 1f))) 
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.7f, 0.3f, 0.9f))) 
            {
                if (ImGui.Button("Allow Anonymous Statistic Collection", new Vector2(ImGui.GetContentRegionAvail().X, 40 * ImGuiHelpers.GlobalScale))) {
                    MetricsService.ReportMetrics(true);
                }
            }
            
            ImGui.Spacing();
            
            
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.3f, 0.8f))) 
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.2f, 0.3f, 1f))) 
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.2f, 0.3f, 0.9f))) 
            {
                if (ImGui.Button("Disable Anonymous Statistic Collection", new Vector2(ImGui.GetContentRegionAvail().X, 25 * ImGuiHelpers.GlobalScale))) {
                    config.AnalyticsOptOut = true;
                    config.Save();
                }

            }
            
            return;
        }
        
        
        SimpleTweaksPlugin.Plugin.PluginConfig.DrawConfigUI();
    }

    public override void OnClose() {
        base.OnClose();
        SimpleTweaksPlugin.Plugin.SaveAllConfig();
        SimpleTweaksPlugin.Plugin.PluginConfig.ClearSearch();
        MetricsService.ReportMetrics(false);
    }
}
