using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;

namespace SimpleTweaksPlugin.Helper {
    public static class ImGuiExt {

        public static void NextRow() {
            while (ImGui.GetColumnIndex() != 0) ImGui.NextColumn();
        }

        public static void SetColumnWidths(int start, float firstWidth, params float[] widths) {
            ImGui.SetColumnWidth(start, firstWidth);
            for (var i = 0; i < widths.Length; i++) {
                ImGui.SetColumnWidth(start + i + 1, widths[i]);
            }
        }

        public static void SetColumnWidths(float firstWidth, params float[] widths) => SetColumnWidths(0, firstWidth, widths);

        public static bool InputByte(string label, ref byte v) {
            var vInt = (int) v;
            if (!ImGui.InputInt(label, ref vInt, 1)) return false;
            if (vInt < byte.MinValue || vInt > byte.MaxValue) return false;
            v = (byte) vInt;
            return true;
        }
        
        public static bool HorizontalAlignmentSelector(string name, ref Alignment selectedAlignment, VerticalAlignment verticalAlignment = VerticalAlignment.Middle) {
            var changed = false;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);

            ImGui.PushID(name);
            ImGui.BeginGroup();
            ImGui.PushFont(UiBuilder.IconFont);

            var alignments = verticalAlignment switch {
                VerticalAlignment.Top => new[] {Alignment.TopLeft, Alignment.Top, Alignment.TopRight},
                VerticalAlignment.Bottom => new[] {Alignment.BottomLeft, Alignment.Bottom, Alignment.BottomRight},
                _ => new[] {Alignment.Left, Alignment.Center, Alignment.Right},
            };
            

            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == alignments[0] ? 0xFF00A5FF: 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignLeft}##{name}")) {
                selectedAlignment = alignments[0];
                changed = true;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == alignments[1] ? 0xFF00A5FF : 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignCenter}##{name}")) {
                selectedAlignment = alignments[1];
                changed = true;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Border, selectedAlignment == alignments[2] ? 0xFF00A5FF : 0x0);
            if (ImGui.Button($"{(char) FontAwesomeIcon.AlignRight}##{name}")) {
                selectedAlignment = alignments[2];
                changed = true;
            }
            ImGui.PopStyleColor();

            ImGui.PopFont();
            ImGui.PopStyleVar();
            ImGui.SameLine();
            ImGui.Text(name);
            ImGui.EndGroup();

            ImGui.PopStyleVar();
            ImGui.PopID();
            return changed;
        }
    }
}
