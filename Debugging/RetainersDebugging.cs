using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class RetainersDebugging : DebugHelper {
    public override string Name => "Retainer Debugging";

    public override void Draw() {
        var retainerManager = RetainerManager.Instance();

        ImGui.Text("Retainer Manager: ");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)retainerManager:X}");

        if (retainerManager == null) return;

        ImGui.Separator();

        for (var i = 0; i < 10; i++) {
            ImGui.Text($"{retainerManager->DisplayOrder[i]}");
        }

        ImGui.Separator();
        DebugManager.PrintOutObject(retainerManager);


    }
}