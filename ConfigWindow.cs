using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace SimpleTweaksPlugin; 

public class ConfigWindow : Window {
    public ConfigWindow() : base("Simple Tweaks Config") {
        Size = new Vector2(600, 400);
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(600, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        SimpleTweaksPlugin.Plugin.PluginConfig.DrawConfigUI();
    }

    public override void OnClose() {
        base.OnClose();
        SimpleTweaksPlugin.Plugin.SaveAllConfig();
        SimpleTweaksPlugin.Plugin.PluginConfig.ClearSearch();
    }
}
