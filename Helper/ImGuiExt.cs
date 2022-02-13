using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;

namespace SimpleTweaksPlugin.Helper; 

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

    private static List<UIColor> uniqueSortedUiForeground = null;
    private static List<UIColor> uniqueSortedUiGlow = null;

    private static void BuildUiColorLists() {
        uniqueSortedUiForeground = new List<UIColor>();
        uniqueSortedUiGlow = new List<UIColor>();
        var s = Service.Data.Excel.GetSheet<UIColor>();
        if (s == null) return;
        foreach (var c in s) {
            if (uniqueSortedUiForeground.All(u => u.UIForeground != c.UIForeground)) {
                uniqueSortedUiForeground.Add(c);
            }
            if (uniqueSortedUiGlow.All(u => u.UIGlow != c.UIGlow)) {
                uniqueSortedUiGlow.Add(c);
            }
        }

        uniqueSortedUiForeground.Sort((a, b) => {
            var aRgb = Common.UiColorToVector4(a.UIForeground);
            var bRgb = Common.UiColorToVector4(b.UIForeground);
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, out var aH, out var aS, out var aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, out var bH, out var bS, out var bV);
            if (aH < bH) return -1;
            if (aH > bH) return 1;
            if (aS < bS) return -1;
            if (aS > bS) return 1;
            if (aV < bV) return -1;
            if (aV > bV) return 1;
            return 0;
        });

        uniqueSortedUiGlow.Sort((a, b) => {
            var aRgb = Common.UiColorToVector4(a.UIGlow);
            var bRgb = Common.UiColorToVector4(b.UIGlow);
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, out var aH, out var aS, out var aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, out var bH, out var bS, out var bV);
            if (aH < bH) return -1;
            if (aH > bH) return 1;
            if (aS < bS) return -1;
            if (aS > bS) return 1;
            if (aV < bV) return -1;
            if (aV > bV) return 1;
            return 0;
        });

    }

    public enum ColorPickerMode {
        ForegroundOnly,
        GlowOnly,
        ForegroundAndGlow,
    }

    public static bool UiColorPicker(string label, ref ushort colourKey, ColorPickerMode mode = ColorPickerMode.ForegroundOnly) {
        var modified = false;

        var glowOnly = mode == ColorPickerMode.GlowOnly;

        var colorSheet = Service.Data.Excel.GetSheet<UIColor>();
        if (colorSheet == null) {
            var i = (int)colourKey;
            if (ImGui.InputInt(label, ref i)) {
                if (i >= ushort.MinValue && i <= ushort.MaxValue) {
                    colourKey = (ushort)i;
                    return true;
                }
            }
            return false;
        }

        var currentColor = colorSheet.GetRow(colourKey);
        if (currentColor == null) currentColor = colorSheet.GetRow(0)!;
        if (currentColor == null) return false;
        var id = ImGui.GetID(label);

        ImGui.SetNextItemWidth(24 * ImGui.GetIO().FontGlobalScale);

        ImGui.PushStyleColor(ImGuiCol.FrameBg, Common.UiColorToVector4(glowOnly ? currentColor.UIGlow : currentColor.UIForeground));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Common.UiColorToVector4(glowOnly ? currentColor.UIGlow : currentColor.UIForeground));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Common.UiColorToVector4(glowOnly ? currentColor.UIGlow : currentColor.UIForeground));

        if (mode == ColorPickerMode.ForegroundAndGlow) {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 4);
            ImGui.PushStyleColor(ImGuiCol.Border, Common.UiColorToVector4(currentColor.UIGlow));
        }

        var comboOpen = ImGui.BeginCombo($"{label}##combo", string.Empty, ImGuiComboFlags.NoArrowButton);
        ImGui.PopStyleColor(3);
        if (mode == ColorPickerMode.ForegroundAndGlow) {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (comboOpen) {

            if (uniqueSortedUiForeground == null || uniqueSortedUiGlow == null) BuildUiColorLists();

            var cl = (glowOnly ? uniqueSortedUiGlow : uniqueSortedUiForeground) ?? new List<UIColor>();

            var sqrt = (int) Math.Sqrt(cl.Count);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1));
            for (var i = 0; i < cl.Count; i++) {
                var c = cl[i];
                if (i != 0 && i % sqrt != 0) ImGui.SameLine();
                if (ImGui.ColorButton($"##ColorPick_{i}_{c.RowId}", Common.UiColorToVector4(glowOnly ? c.UIGlow : c.UIForeground), ImGuiColorEditFlags.NoTooltip)) {
                    colourKey = (ushort)c.RowId;
                    modified = true;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.PopStyleVar();


            ImGui.EndCombo();
        }

        return modified;
    }

}