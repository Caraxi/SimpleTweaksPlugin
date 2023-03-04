using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;

namespace SimpleTweaksPlugin; 

public class DebugWindow : Window {
    public DebugWindow() : base("SimpleTweaksPlugin - Debug") {
        WindowName = $"SimpleTweaksPlugin - Debug [{Assembly.GetExecutingAssembly().GetName().Version}] - Client Structs Version#{FFXIVClientStructs.Interop.Resolver.Version}###stDebugMenu";
        
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
        DebugManager.DrawDebugWindow();
    }
}
