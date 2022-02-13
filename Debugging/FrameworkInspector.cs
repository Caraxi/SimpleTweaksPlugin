using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class FrameworkInspector : DebugHelper {
    public override string Name => "Framework Inspector";

    public override void Draw() {
        var framework = Framework.Instance();
        var serverTime = Framework.GetServerTime();
        ImGui.Text($"Server Time: {serverTime}");
        DebugManager.ClickToCopyText($"{(ulong)framework:X}");
        ImGui.SameLine();
        DebugManager.PrintOutObject(framework);
    }
}