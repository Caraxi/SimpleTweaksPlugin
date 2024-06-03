using System;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
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
        ImGui.Text($"{raptureGearsetModule->UserFileEvent.FileNameString}");
            
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

            ImGui.Text("MainHand"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.MainHand).ItemId}]");
            ImGui.Text("OffHand"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.OffHand).ItemId}]");
            ImGui.Text("Head"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Head).ItemId}]");
            ImGui.Text("Body"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Body).ItemId}]");
            ImGui.Text("Hands"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Hands).ItemId}]");
            ImGui.Text("Belt"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Belt).ItemId}]");
            ImGui.Text("Legs"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Legs).ItemId}]");
            ImGui.Text("Feet"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Feet).ItemId}]");
            ImGui.Text("Ears"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Ears).ItemId}]");
            ImGui.Text("Neck"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Neck).ItemId}]");
            ImGui.Text("Wrists"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.Wrists).ItemId}]");
            ImGui.Text("RingRight"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.RingRight).ItemId}]");
            ImGui.Text("RingLeft"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.RingLeft).ItemId}]");
            ImGui.Text("SoulStone"); ImGui.SameLine(); ImGui.Text($"[{gearset->GetItem(RaptureGearsetModule.GearsetItemIndex.SoulStone).ItemId}]");
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