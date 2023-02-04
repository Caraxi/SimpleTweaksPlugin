using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace SimpleTweaksPlugin; 

public class ConfigWindow : Window {
    public ConfigWindow() : base("Simple Tweaks Config") {
        
    }

    public override void PreDraw() {
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = ImGuiHelpers.ScaledVector2(600, 200),
            MaximumSize = ImGuiHelpers.ScaledVector2(800, 800)
        };
    }

    public override void Draw() {
        SimpleTweaksPlugin.Plugin.PluginConfig.DrawConfigUI();
    }

    public override void OnClose() {
        base.OnClose();
        SimpleTweaksPlugin.Plugin.SaveAllConfig();
    }
}
