using ImGuiNET;

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
    }
}
