using System.Collections.Generic;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging {
    public class TargetDebugging : DebugHelper {
        public override void Draw() {

            var targets = Service.Targets;
            
            
            ImGui.Text("Current Target:");
            
            
            if (targets.Target != null) DebugManager.PrintOutObject(targets.Target, (ulong)targets.Target.Address.ToInt64(), new List<string>() { "Target", "Current"});
            if (targets.FocusTarget != null) DebugManager.PrintOutObject(targets.FocusTarget, (ulong)targets.FocusTarget.Address.ToInt64(), new List<string>(){ "Target", "Focus"});
            if (targets.PreviousTarget != null) DebugManager.PrintOutObject(targets.PreviousTarget, (ulong)targets.PreviousTarget.Address.ToInt64(), new List<string>(){ "Target", "Prev"});
            if (targets.MouseOverTarget != null) DebugManager.PrintOutObject(targets.MouseOverTarget, (ulong)targets.MouseOverTarget.Address.ToInt64(), new List<string>(){ "Target", "MouseOver"});
            
            


        }

        public override string Name => "Targets";
    }
}
