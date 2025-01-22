using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging;

public class TargetDebugging : DebugHelper {

    private unsafe void PrintTarget(GameObject* gameObject) {
        if (gameObject->IsCharacter()) {
            DebugManager.PrintOutObject((Character*)gameObject);
            return;
        }
        DebugManager.PrintOutObject(gameObject);
    }
    
    public override unsafe void Draw() {
        var targets = Service.Targets;
        var targetSystem = TargetSystem.Instance();

        ImGui.Text("Current Target:");
        if (targets.Target != null) DebugManager.PrintOutObject(targets.Target, (ulong)targets.Target.Address.ToInt64(), new List<string>() { "Target", "Current" });
        if (targetSystem->Target != null) PrintTarget(targetSystem->Target);

        ImGui.Text("Focus Target:");
        if (targets.FocusTarget != null) DebugManager.PrintOutObject(targets.FocusTarget, (ulong)targets.FocusTarget.Address.ToInt64(), new List<string>() { "Target", "Focus" });
        if (targetSystem->FocusTarget != null) PrintTarget(targetSystem->FocusTarget);

        ImGui.Text("Previous Target:");
        if (targets.PreviousTarget != null) DebugManager.PrintOutObject(targets.PreviousTarget, (ulong)targets.PreviousTarget.Address.ToInt64(), new List<string>() { "Target", "Prev" });
        if (targetSystem->PreviousTarget != null) PrintTarget(targetSystem->PreviousTarget);

        ImGui.Text("Mouse Over Target:");
        if (targets.MouseOverTarget != null) DebugManager.PrintOutObject(targets.MouseOverTarget, (ulong)targets.MouseOverTarget.Address.ToInt64(), new List<string>() { "Target", "MouseOver" });
        if (targetSystem->MouseOverTarget != null) PrintTarget(targetSystem->MouseOverTarget);
    }

    public override string Name => "Targets";
}
