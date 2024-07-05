using System.Numerics;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class RetainersDebugging : DebugHelper {
    public override string Name => "Retainer Debugging";

    public override void Draw() {
        var retainerManager = RetainerManager.Instance();

        ImGui.Text("Retainer Manager: ");
        ImGui.SameLine();
        DebugManager.ClickToCopy(retainerManager);
        if (retainerManager == null) return;
        ImGui.SameLine();
        DebugManager.PrintOutObject(retainerManager);
        ImGui.Separator();
        ImGui.Text("Order: ");
        for (var i = 0; i < 10 && i < retainerManager->Retainers.Length; i++) {
            ImGui.SameLine();
            ImGui.Text($"{retainerManager->DisplayOrder[i]:X2}");
        }

        ImGui.Separator();

        if (ImGui.BeginTable("retainers", 4)) {
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Struct", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var r in retainerManager->Retainers.PointerEnumerator()) {
                if (r->RetainerId == 0) continue;

                ImGui.TableNextColumn();
                DebugManager.ClickToCopyText($"{r->RetainerId:X}");
                ImGui.TableNextColumn();
                ImGui.Text($"{r->NameString}");
                ImGui.TableNextColumn();

                if (r->ClassJob == 0) {
                    ImGui.Text("No Class");
                } else {
                    var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup((uint)(62100 + r->ClassJob))).GetWrapOrDefault();
                    if (icon != null) {
                        ImGui.Image(icon.ImGuiHandle, new Vector2(24));
                    } else {
                        ImGui.Text($"[{r->ClassJob}]");
                    }

                    ImGui.SameLine();
                    ImGui.Text($"{r->Level}");
                }

                ImGui.TableNextColumn();
                DebugManager.PrintOutObject(r);
            }

            ImGui.EndTable();
        }
    }
}
