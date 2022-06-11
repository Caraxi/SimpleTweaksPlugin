using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class InventoryDebug : DebugHelper {
    private InventoryType inventoryType;

    public override void Draw() {
        DebugManager.ClickToCopyText($"{(ulong)InventoryManager.Instance():X}");
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

                var container = InventoryManager.Instance()->GetInventoryContainer(inventoryType);

                ImGui.PopItemWidth();
                    
                    
                if (container != null) {
                        
                    ImGui.Text($"Container Address:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)container:X}");
                        
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(*container, (ulong) container, new List<string>());

                    if (ImGui.TreeNode("Items##containerItems")) {
                        for (var i = 0; i < container->Size; i++) {
                            var item = container->GetInventorySlot(i);
                            DebugManager.ClickToCopy(item);
                            ImGui.SameLine();
                            var itemName = item->ItemID == 0 ? string.Empty : Service.Data.Excel.GetSheet<Item>()?.GetRow(item->ItemID)?.Name?.RawString;
                            DebugManager.PrintOutObject(*item, (ulong) item, new List<string> {$"Items[{i}]"},false, $"[{i:00}] {itemName ?? "<Not Found>"}" );
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