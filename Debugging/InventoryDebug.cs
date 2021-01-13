using System;
using System.Collections.Generic;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class InventoryDebug : DebugHelper {
        private int containerId = 0;
        private int slotId = 0;
        
        public override void Draw() {
            ImGui.PushItemWidth(200);
            ImGui.InputInt("Container ID##debugInventoryContainer", ref containerId);
            ImGui.InputInt("Slot ID##debugInventoryContainer", ref slotId);
            ImGui.PopItemWidth();
            var container = Common.GetContainer(containerId); 
            
            if (container != IntPtr.Zero) {
                ImGui.Text($"Container {containerId} Address:");
                ImGui.SameLine();
                DebugManager.ClickToCopyText($"{container.ToInt64():X}");

                var slot = Common.GetContainerItem(container, slotId);

                if (slot != null) {
                    
                    ImGui.Text($"Slot {slotId} Address:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)slot:X}");
                    
                    DebugManager.PrintOutObject(*slot, (ulong) slot, new List<string>(), true);
                } else {
                    ImGui.Text("Slot not found.");
                }
            } else {
                ImGui.Text("Container not found.");
            }
        }

        public override string Name => "Inventory Debugging";
    }
}
