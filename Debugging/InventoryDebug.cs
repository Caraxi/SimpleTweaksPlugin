using System;
using System.Collections.Generic;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class InventoryDebug : DebugHelper {
    private InventoryType inventoryType;

    public override void Draw() {
        DebugManager.ClickToCopyText($"{Common.InventoryManagerAddress.ToInt64():X}");
        if (ImGui.BeginTabBar("inventoryDebuggingTabs")) {
            if (ImGui.BeginTabItem("Container/Slot")) {
                ImGui.PushItemWidth(200);
                if (ImGui.BeginCombo("###containerSelect", $"{inventoryType} [{(int)inventoryType}]")) {

                    foreach (var i in (InventoryType[]) Enum.GetValues(typeof(InventoryType))) {
                        if (ImGui.Selectable($"{i} [{(int) i}]##inventoryTypeSelect", i == inventoryType)) {
                            inventoryType = i;
                        }
                    }
                    ImGui.EndCombo();
                }
                    
                var container = Common.GetContainer(inventoryType); 

                ImGui.PopItemWidth();
                    
                    
                if (container != null) {
                        
                    ImGui.Text($"Container Address:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)container:X}");
                        
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(*container, (ulong) container, new List<string>());

                    if (ImGui.TreeNode("Items##containerItems")) {
                            
                        for (var i = 0; i < container->SlotCount; i++) {
                            var item = container->Items[i];
                            var itemAddr = ((ulong) container->Items) + (ulong)sizeof(InventoryItem) * (ulong)i;
                            DebugManager.ClickToCopyText($"{itemAddr:X}");
                            ImGui.SameLine();
                            DebugManager.PrintOutObject(item, (ulong) &item, new List<string> {$"Items[{i}]"},false, $"[{i:00}] {item.Item?.Name ?? "<Not Found>"}" );
                        }
                        ImGui.TreePop();
                    }
                } else {
                    ImGui.Text("Container not found.");
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

    }

    public override string Name => "Inventory Debugging";
}