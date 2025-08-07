using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Debugging;

public class GraphicFontDebug : DebugHelper {
    public override string Name => "Graphic Font";

    private float maxWidthIcon = 50;

    public override void Draw() {
        var styles = Enum.GetValues<Utility.GraphicFont.IconStyle>();

        if (!ImGui.BeginTable("graphicFontTable", styles.Length + 2, ImGuiTableFlags.BordersInnerH)) return;

        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 300 * ImGuiHelpers.GlobalScale);
        foreach (var s in styles) {
            ImGui.TableSetupColumn($"{s}", ImGuiTableColumnFlags.WidthFixed, maxWidthIcon * 2 * ImGuiHelpers.GlobalScale);
        }

        ImGui.TableSetupColumn("Position & Size", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();

        foreach (var e in GraphicFont.FontIcons.Icons) {
            if (!e.IsValid()) continue;
            if (e.Size.X > maxWidthIcon) maxWidthIcon = e.Size.X;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"#{e.ID}");
            ImGui.TextUnformatted($"{(BitmapFontIcon)e.ID}");

            foreach (var s in styles) {
                ImGui.TableNextColumn();
                e.Draw(true, s);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{e.Position.X},{e.Position.Y}");
            ImGui.TextUnformatted($"{e.Size.X},{e.Size.Y}");
        }

        ImGui.EndTable();
    }
}
