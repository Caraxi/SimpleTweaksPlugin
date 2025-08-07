using System;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class GearsetDebug : DebugHelper {
  

    public override string Name => "Gearset Debugging";
        
    public override void Draw() {

        var raptureGearsetModule = RaptureGearsetModule.Instance();
            
        ImGui.Text("RaptureGearsetModule:");
        ImGui.SameLine();
        DebugManager.ClickToCopyText($"{(ulong)raptureGearsetModule:X}");
        ImGui.SameLine();
        ImGui.Text(raptureGearsetModule->UserFileEvent.FileNameString);
            
        ImGui.Columns(6);
        ImGui.Text($"##");
        ImGuiExt.SetColumnWidths(35f, 120);
        ImGui.NextColumn();
        ImGui.Text("Address");
        ImGui.NextColumn();
        ImGui.Text("Name");
        ImGui.NextColumn();
        ImGui.Text("Items");
        ImGui.NextColumn();
        ImGui.Text("Flags");
        ImGui.NextColumn();
        ImGui.Text("Object");
        ImGuiExt.NextRow();
        ImGuiExt.NextRow();
        ImGui.Separator();
        ImGui.Separator();
            
            
        for (var i = 0; i < 101; i++) {
            var gearset = raptureGearsetModule->GetGearset(i);
            if (gearset == null || gearset->Id != i) break;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                
            ImGui.Text($"{gearset->Id:00}");
            ImGui.NextColumn();
            DebugManager.ClickToCopyText($"{(ulong) gearset:X}");
            ImGui.NextColumn();
            ImGui.Text(gearset->NameString);
            ImGui.NextColumn();

            for (var s = 0; s < gearset->Items.Length; s++) {
                var item = gearset->Items[s];
                ImGui.Text($"{(RaptureGearsetModule.GearsetItemIndex)s}: {item.ItemId}");
            }
            
            ImGui.NextColumn();

            foreach (RaptureGearsetModule.GearsetFlag r in Enum.GetValues(typeof(RaptureGearsetModule.GearsetFlag))) {
                if (gearset->Flags.HasFlag(r)) {
                    ImGui.Text(r.ToString());
                }
            }

            ImGui.NextColumn();
            DebugManager.PrintOutObject(gearset);
            
            ImGuiExt.NextRow();
            ImGuiExt.NextRow();
            ImGui.Separator();
        }
            
        ImGui.Columns();
            
    }
        
}