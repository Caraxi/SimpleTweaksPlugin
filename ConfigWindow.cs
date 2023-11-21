using System.Numerics;
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
        SimpleTweaksPlugin.Plugin.PluginConfig.DrawConfigUI();
    }

    public override void OnClose() {
        base.OnClose();
        SimpleTweaksPlugin.Plugin.SaveAllConfig();
        SimpleTweaksPlugin.Plugin.PluginConfig.ClearSearch();
        MetricsService.ReportMetrics();
    }
}
