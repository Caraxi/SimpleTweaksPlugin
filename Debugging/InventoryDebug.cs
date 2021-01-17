using System;
using System.Collections.Generic;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class InventoryDebug : DebugHelper {
        private int containerId;
        private int slotId;
        
        public override void Draw() {
            DebugManager.ClickToCopyText($"{Common.InventoryManagerAddress.ToInt64():X}");
            if (ImGui.BeginTabBar("inventoryDebuggingTabs")) {
                if (ImGui.BeginTabItem("Container/Slot")) {
                    
                    ImGui.PushItemWidth(200);
                    ImGui.InputInt("Container ID##debugInventoryContainer", ref containerId);
                    ImGui.InputInt("Slot ID##debugInventoryContainer", ref slotId);
                    ImGui.PopItemWidth();
                    var container = Common.GetContainer((InventoryType) containerId); 
            
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
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Sorting")) {

                    var module = UiHelper.UiModule.ItemOrderModule;
                    
                    ImGui.Text("Item Order Module:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong) module:X}");
                    
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(module, (ulong) module, new List<string>());
                    
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Finder")) {

                    var module = UiHelper.UiModule.ItemFinderModule;

                    ImGui.Text("ItemFinderModule:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong) module:X}");
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(module, (ulong) module, new List<string>());
                }
                
                
                ImGui.EndTabBar();
            }

        }

        public override string Name => "Inventory Debugging";
    }
}
