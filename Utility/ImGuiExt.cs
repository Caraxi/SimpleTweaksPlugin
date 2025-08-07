using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Enums;

namespace SimpleTweaksPlugin.Utility; 

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

    private static List<UIColor> uniqueSortedUiForeground;
    private static List<UIColor> uniqueSortedUiGlow;

    private static void BuildUiColorLists() {
        uniqueSortedUiForeground = new List<UIColor>();
        uniqueSortedUiGlow = new List<UIColor>();
        var s = Service.Data.Excel.GetSheet<UIColor>();
        if (s == null) return;
        foreach (var c in s) {
            if (uniqueSortedUiForeground.All(u => u.Dark != c.Dark)) {
                uniqueSortedUiForeground.Add(c);
            }
            if (uniqueSortedUiGlow.All(u => u.Light != c.Light)) {
                uniqueSortedUiGlow.Add(c);
            }
        }

        uniqueSortedUiForeground.Sort((a, b) => {
            var aRgb = Common.UiColorToVector4(a.Dark);
            var bRgb = Common.UiColorToVector4(b.Dark);
            float aH = 0f, aS = 0f, aV = 0f, bH = 0f, bS = 0f, bV = 0f;
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, ref aH, ref aS, ref aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, ref bH, ref bS, ref bV);
            if (aH < bH) return -1;
            if (aH > bH) return 1;
            if (aS < bS) return -1;
            if (aS > bS) return 1;
            if (aV < bV) return -1;
            if (aV > bV) return 1;
            return 0;
        });

        uniqueSortedUiGlow.Sort((a, b) => {
            var aRgb = Common.UiColorToVector4(a.Light);
            var bRgb = Common.UiColorToVector4(b.Light);
            float aH = 0f, aS = 0f, aV = 0f, bH = 0f, bS = 0f, bV = 0f;
            ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, ref aH, ref aS, ref aV);
            ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, ref bH, ref bS, ref bV);
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

        if (!colorSheet.TryGetRow(colourKey, out var currentColor)) {
            if (!colorSheet.TryGetRow(0, out currentColor)) {
                return false;
            }
        }

        var id = ImGui.GetID(label);

        ImGui.SetNextItemWidth(24 * ImGui.GetIO().FontGlobalScale);

        ImGui.PushStyleColor(ImGuiCol.FrameBg, Common.UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Common.UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Common.UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));

        if (mode == ColorPickerMode.ForegroundAndGlow) {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 4);
            ImGui.PushStyleColor(ImGuiCol.Border, Common.UiColorToVector4(currentColor.Light));
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
                if (ImGui.ColorButton($"##ColorPick_{i}_{c.RowId}", Common.UiColorToVector4(glowOnly ? c.Light : c.Dark), ImGuiColorEditFlags.NoTooltip)) {
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

    public static void ShadowedText(string text, int size = 1, Vector4? shadowColour = null) {
        shadowColour ??= new Vector4(0, 0, 0, 0.75f);
        var pos = ImGui.GetCursorPos();
        for (var x = -size; x <= size; x++) {
            for (var y = -size; y <= size; y++) {
                ImGui.SetCursorPos(pos + new Vector2(x, y));
                ImGui.TextColored(shadowColour.Value, text);
                ImGui.SameLine();
            }
        }
        ImGui.SetCursorPos(pos);
        ImGui.Text(text);
    }

    public static Vector2 GetWindowContentRegionSize() {
        return ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin();
    }

    public static bool IconButton(string id, FontAwesomeIcon icon) {
        try {
            ImGui.PushFont(UiBuilder.IconFont);
            return ImGui.Button($"{(char)icon}##{id}", new Vector2(ImGui.GetTextLineHeight()) + ImGui.GetStyle().FramePadding * 2);
        } finally {
            ImGui.PopFont();
        }
    }

    public static void TextWrappedDisabled(string text) {
        try {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            ImGui.TextWrapped(text);
        } finally {
            ImGui.PopStyleColor();
        }
    }

    public static bool ModifierFlagEditor(ref ModifierFlag tweakConfigPanModifier, bool allowNone = false) {
        var e = false;
        using (ImRaii.Group()) {
            var btnSize = new Vector2(ImGui.GetTextLineHeightWithSpacing() * 2, ImGui.GetTextLineHeightWithSpacing()) + ImGui.GetStyle().FramePadding * 2;
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)) {
                using (ImRaii.PushColor(ImGuiCol.Text, tweakConfigPanModifier.HasFlag(ModifierFlag.Shift) ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed)) {
                    if (ImGui.Button($"SHIFT", btnSize)) {
                        tweakConfigPanModifier ^= ModifierFlag.Shift;
                        if (!allowNone && tweakConfigPanModifier == 0) tweakConfigPanModifier = ModifierFlag.Shift;
                        e = true;
                    }
                }
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, tweakConfigPanModifier.HasFlag(ModifierFlag.Ctrl) ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed)) {
                    if (ImGui.Button("CTRL", btnSize)) {
                        tweakConfigPanModifier ^= ModifierFlag.Ctrl;
                        if (!allowNone && tweakConfigPanModifier == 0) tweakConfigPanModifier = ModifierFlag.Ctrl;
                        e = true;
                    }
                }
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, tweakConfigPanModifier.HasFlag(ModifierFlag.Alt) ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed)) {
                    if (ImGui.Button("ALT", btnSize)) {
                        tweakConfigPanModifier ^= ModifierFlag.Alt;
                        if (!allowNone && tweakConfigPanModifier == 0) tweakConfigPanModifier = ModifierFlag.Alt;
                        e = true;
                    }
                }
            }
        }

        return e;
    }
}