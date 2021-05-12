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

        public static bool InputByte(string label, ref byte v) {
            var vInt = (int) v;
            if (!ImGui.InputInt(label, ref vInt, 1)) return false;
            if (vInt < byte.MinValue || vInt > byte.MaxValue) return false;
            v = (byte) vInt;
            return true;
        }
    }
}
