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
        ImGui.Text($"{Encoding.ASCII.GetString(raptureGearsetModule->ModuleName, 15)}");
            
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
        ImGuiExt.NextRow();
        ImGuiExt.NextRow();
        ImGui.Separator();
        ImGui.Separator();
            
            
        for (var i = 0; i < 101; i++) {
            var gearset = raptureGearsetModule->Gearset[i];
            if (gearset->ID != i) break;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                
            ImGui.Text($"{gearset->ID:00}");
            ImGui.NextColumn();
            DebugManager.ClickToCopyText($"{(ulong) gearset:X}");
            ImGui.NextColumn();
            ImGui.Text(Encoding.UTF8.GetString(gearset->Name, 0x2F));
            ImGui.NextColumn();

            ImGui.Text("MainHand"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.MainHand.ItemID}]");
            ImGui.Text("OffHand"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.OffHand.ItemID}]");
            ImGui.Text("Head"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Head.ItemID}]");
            ImGui.Text("Body"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Body.ItemID}]");
            ImGui.Text("Hands"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Hands.ItemID}]");
            ImGui.Text("Belt"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Belt.ItemID}]");
            ImGui.Text("Legs"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Legs.ItemID}]");
            ImGui.Text("Feet"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Feet.ItemID}]");
            ImGui.Text("Ears"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Ears.ItemID}]");
            ImGui.Text("Neck"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Neck.ItemID}]");
            ImGui.Text("Wrists"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.Wrists.ItemID}]");
            ImGui.Text("RingRight"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.RingRight.ItemID}]");
            ImGui.Text("SoulStone"); ImGui.SameLine(); ImGui.Text($"[{gearset->ItemsStruct.SoulStone.ItemID}]");
            ImGui.NextColumn();

            foreach (RaptureGearsetModule.GearsetFlag r in Enum.GetValues(typeof(RaptureGearsetModule.GearsetFlag))) {
                if (gearset->Flags.HasFlag(r)) {
                    ImGui.Text(r.ToString());
                }
            }

            ImGuiExt.NextRow();
            ImGuiExt.NextRow();
            ImGui.Separator();
        }
            
        ImGui.Columns();
            
    }
        
}