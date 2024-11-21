using System.Reflection;
using Dalamud.Interface.Utility;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin;

public class DebugWindow : SimpleWindow {
    public DebugWindow() : base("SimpleTweaksPlugin - Debug") {
        WindowName = $"SimpleTweaksPlugin - Debug [{Assembly.GetExecutingAssembly().GetName().Version}] - Client Structs Version#{Common.ClientStructsVersion}###stDebugMenu";
        
        Size = ImGuiHelpers.ScaledVector2(500, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = ImGuiHelpers.ScaledVector2(350),
            MaximumSize = ImGuiHelpers.ScaledVector2(2000)
        };
    }

    public override void Draw() {
        base.Draw();
        DebugManager.DrawDebugWindow();
    }
}
