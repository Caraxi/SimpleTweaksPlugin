using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.ExtraPayloads;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Custom Free Company Tags")]
[TweakDescription("Allows hiding or customizing Free Company and Wanderer tags.")]
[TweakAutoConfig]
public unsafe class CustomFreeCompanyTags : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public Dictionary<string, TagCustomization> FcCustomizations = new();
        public TagCustomization DefaultCustomization = new();
        public TagCustomization WandererCustomization = new() { Enabled = false, Replacement = "<crossworldicon><homeworld>" };
        public TagCustomization TravellerCustomization = new() { Enabled = false, Replacement = "<crossworldicon><homeworld>" };
    }

    public class TagCustomization {
        public bool Enabled;
        public string Replacement = string.Empty;
        public bool HideQuoteMarks;
        public bool OwnLine;
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected override void Setup() {
        AddChangelog("1.8.7.0", "Added option to display FC tags on a separate line to character name.");
        AddChangelog("1.8.7.2", "Removed 'Hide in Duty' option from Wanderer. This is now a vanilla game option.");
        AddChangelog("1.8.9.0", "Added support for full RGB colours.");
        AddChangelog("1.8.9.0", "Added an icon viewer for supported icons.");
        AddChangelog("1.8.9.1", "Fix some issues with glow colours.");
        AddChangelog("1.8.9.2", "Fixed icon-only tags not displaying.");
    }

    protected override void Enable() {
        Service.NamePlateGui.OnDataUpdate += NamePlateGuiOnOnDataUpdate;
    }

    private void NamePlateGuiOnOnDataUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers) {
        foreach (var h in handlers) {
            if (h.PlayerCharacter == null) continue;

            var battleChara = (BattleChara*)h.PlayerCharacter.Address;
            try {
                var customization = Config.DefaultCustomization;
                string companyTag = string.Empty;
                if (battleChara->Character.HomeWorld != battleChara->Character.CurrentWorld) {
                    // Wanderer
                    var w = Service.Data.Excel.GetSheet<World>().GetRowOrNull(battleChara->Character.HomeWorld);
                    if (w == null || w.Value.RowId == 0 || w.Value.DataCenter.RowId == Service.ClientState.LocalPlayer.CurrentWorld.Value.DataCenter.RowId) {
                        customization = Config.WandererCustomization;
                    } else {
                        customization = Config.TravellerCustomization;
                    }
                } else {
                    companyTag = battleChara->Character.FreeCompanyTagString;

                    customization = companyTag.Length switch {
                        <= 0 => null,
                        > 0 when Config.FcCustomizations.ContainsKey(companyTag) => Config.FcCustomizations[companyTag],
                        _ => customization
                    };
                }

                if (customization is { Enabled: true }) {
                    if (customization.Replacement.Trim().Length == 0) {
                        h.RemoveFreeCompanyTag();
                    } else {
                        var payloads = new List<Payload>();
                        if (customization.OwnLine)
                            payloads.Add(new NewLinePayload());
                        if (!customization.HideQuoteMarks)
                            payloads.Add(new TextPayload(" «"));

                        var cText = string.Empty;

                        var resetForeground = false;
                        var resetGlow = false;
                        var resetItalic = false;

                        var resetHexForegrond = false;
                        var resetHexGlow = false;
                        Vector3? hexGlow = null;

                        foreach (var t in customization.Replacement) {
                            switch (t) {
                                case '<': {
                                    if (cText.Length > 0) {
                                        payloads.Add(new TextPayload(cText));
                                    }

                                    cText = "<";
                                    break;
                                }
                                case '>' when cText.Length > 0 && cText[0] == '<': {
                                    cText += '>';
                                    switch (cText.ToLower()) {
                                        case "<crossworldicon>": {
                                            payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                                            break;
                                        }
                                        case "<homeworld>": {
                                            var world = Service.Data.Excel.GetSheet<World>().GetRowOrNull(battleChara->Character.HomeWorld);

                                            payloads.Add(new TextPayload(world?.Name.ExtractText() ?? $"UnknownWorld#{battleChara->Character.HomeWorld}"));
                                            break;
                                        }
                                        case "<level>": {
                                            payloads.Add(new TextPayload(battleChara->Character.CharacterData.Level.ToString()));
                                            break;
                                        }
                                        case "<fctag>": {
                                            payloads.Add(new TextPayload(companyTag));
                                            break;
                                        }
                                        case "<i>": {
                                            payloads.Add(new EmphasisItalicPayload(true));
                                            resetItalic = true;
                                            break;
                                        }
                                        case "</i>": {
                                            payloads.Add(new EmphasisItalicPayload(false));
                                            resetItalic = false;
                                            break;
                                        }
                                        case "</color>":
                                        case "</colour>": {
                                            if (resetHexForegrond) {
                                                if (hexGlow != null) {
                                                    payloads.Add(new GlowEndPayload().AsRaw());
                                                }

                                                payloads.Add(new ColorEndPayload().AsRaw());
                                                if (hexGlow != null) {
                                                    payloads.Add(new GlowPayload(hexGlow.Value).AsRaw());
                                                }

                                                resetHexForegrond = false;
                                            }

                                            if (resetForeground) {
                                                payloads.Add(new UIForegroundPayload(0));
                                                resetForeground = false;
                                            }

                                            break;
                                        }
                                        case "</glow>": {
                                            if (resetHexGlow) {
                                                payloads.Add(new GlowEndPayload().AsRaw());
                                                resetHexGlow = false;
                                                hexGlow = null;
                                            }

                                            if (resetGlow) {
                                                payloads.Add(new UIGlowPayload(0));
                                                resetGlow = false;
                                            }

                                            break;
                                        }
                                        case { } s when s.StartsWith("<color:"): {
                                            var k = s.Substring(7, s.Length - 8);

                                            if (TryGetColorFromHex(k, out var hexColor)) {
                                                if (resetHexForegrond) {
                                                    payloads.Add(new ColorEndPayload().AsRaw());
                                                }

                                                payloads.Add(new ColorPayload(hexColor).AsRaw());
                                                resetHexForegrond = true;
                                            } else {
                                                if (ushort.TryParse(k, out var colorKey)) {
                                                    payloads.Add(new UIForegroundPayload(colorKey));
                                                    resetForeground = colorKey != 0;
                                                } else {
                                                    payloads.Add(new TextPayload(cText));
                                                }
                                            }

                                            break;
                                        }
                                        case { } s when s.StartsWith("<colour:"): {
                                            var k = s.Substring(8, s.Length - 9);
                                            if (TryGetColorFromHex(k, out var hexColor)) {
                                                if (hexGlow != null) {
                                                    payloads.Add(new GlowEndPayload().AsRaw());
                                                }

                                                if (resetHexForegrond) {
                                                    payloads.Add(new ColorEndPayload().AsRaw());
                                                }

                                                payloads.Add(new ColorPayload(hexColor).AsRaw());
                                                if (hexGlow != null) {
                                                    payloads.Add(new GlowPayload(hexGlow.Value).AsRaw());
                                                }

                                                resetHexForegrond = true;
                                            } else {
                                                if (ushort.TryParse(k, out var colorKey)) {
                                                    payloads.Add(new UIForegroundPayload(colorKey));
                                                    resetForeground = colorKey != 0;
                                                } else {
                                                    payloads.Add(new TextPayload(cText));
                                                }
                                            }

                                            break;
                                        }
                                        case { } s when s.StartsWith("<glow:"): {
                                            var k = s.Substring(6, s.Length - 7);
                                            if (TryGetColorFromHex(k, out var hexColor)) {
                                                if (resetHexGlow) {
                                                    payloads.Add(new GlowEndPayload().AsRaw());
                                                }

                                                hexGlow = hexColor;
                                                payloads.Add(new GlowPayload(hexColor).AsRaw());
                                                resetHexGlow = true;
                                            } else {
                                                if (ushort.TryParse(k, out var colorKey)) {
                                                    payloads.Add(new UIGlowPayload(colorKey));
                                                    resetGlow = colorKey != 0;
                                                } else {
                                                    payloads.Add(new TextPayload(cText));
                                                }
                                            }

                                            break;
                                        }
                                        case { } s when s.StartsWith("<icon:"): {
                                            var k = s.Substring(6, s.Length - 7);
                                            if (uint.TryParse(k, out var iconKey)) {
                                                payloads.Add(new IconPayload((BitmapFontIcon)iconKey));
                                                resetGlow = iconKey != 0;
                                            } else {
                                                payloads.Add(new TextPayload(cText));
                                            }

                                            break;
                                        }
                                        default: {
                                            payloads.Add(new TextPayload(cText));
                                            break;
                                        }
                                    }

                                    cText = string.Empty;
                                    break;
                                }
                                default: {
                                    cText += t;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(cText)) {
                            payloads.Add(new TextPayload(cText));
                        }

                        if (resetForeground) payloads.Add(new UIForegroundPayload(0));
                        if (resetGlow) payloads.Add(new UIGlowPayload(0));
                        if (resetItalic) payloads.Add(new EmphasisItalicPayload(false));
                        if (resetHexForegrond) payloads.Add(new ColorEndPayload());
                        if (resetHexGlow) payloads.Add(new GlowEndPayload());

                        if (!customization.HideQuoteMarks)
                            payloads.Add(new TextPayload("»"));

                        var seString = new SeString(payloads);
                        if (string.IsNullOrWhiteSpace(seString.TextValue) && !payloads.Any(p => p is IconPayload)) {
                            h.RemoveFreeCompanyTag();
                        } else {
                            h.FreeCompanyTag = seString;
                        }
                    }
                }
            } catch (Exception ex) {
                if (!errored) {
                    errored = true;
                    Plugin.Error(this, ex, true);
                }
            }
        }
    }

    protected override void Disable() {
        Service.NamePlateGui.OnDataUpdate -= NamePlateGuiOnOnDataUpdate;
    }

    private bool TryGetColorFromHex(string str, out Vector3 hexColor) {
        hexColor = Vector3.One;
        if (str.Length != 7) return false;
        if (str[0] != '#') return false;
        if (str.Contains(' ')) return false;

        if (!byte.TryParse(str.AsSpan(1, 2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var r) || !byte.TryParse(str.AsSpan(3, 2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var g) || !byte.TryParse(str.AsSpan(5, 2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var b)) return false;

        hexColor = new Vector3(r / 255f, g / 255f, b / 255f);
        return true;
    }

    private bool errored;

    private string defaultString = "Default (FC)";
    private string wandererString = "Wanderer";
    private string travellerString = "Traveller";
    private string newFcName = string.Empty;

    protected void DrawConfig() {
        if (ImGui.BeginTable("fcList#noFreeCompanyOnNamePlate", 4)) {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("Replace"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("FC Tag"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 75 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("Replacement"), ImGuiTableColumnFlags.NoClip);
            ImGui.TableHeadersRow();

            string deleteKey = null;
            string renameKey = null;

            foreach (var fc in Config.FcCustomizations.OrderBy(k => k.Key)) {
                var k = fc.Key;
                var edit = TagCustomizationEditor(ref k, fc.Value);
                if (edit == ChangeType.Rename) {
                    if (!Config.FcCustomizations.ContainsKey(k)) {
                        deleteKey = fc.Key;
                        renameKey = k;
                    }
                } else if (edit == ChangeType.Delete) {
                    deleteKey = fc.Key;
                }
            }

            if (!string.IsNullOrEmpty(deleteKey)) {
                if (!string.IsNullOrEmpty(renameKey)) {
                    Config.FcCustomizations.Add(renameKey, Config.FcCustomizations[deleteKey]);
                }

                Config.FcCustomizations.Remove(deleteKey);
            }

            TagCustomizationEditor(ref wandererString, Config.WandererCustomization, false);
            TagCustomizationEditor(ref travellerString, Config.TravellerCustomization, false);
            TagCustomizationEditor(ref defaultString, Config.DefaultCustomization, false);
            // TagCustomizationEditor(ref noCompanyString, Config.NoCompanyCustomization, false);

            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.Text("Add FC:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var addNew = ImGui.InputText($"##fcList#{GetType().Name}_new_name", ref newFcName, 5, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.TableNextColumn();
            if (ImGui.Button(LocString("AddButton", "Add") + $"##fcList#{GetType().Name}_new_button") || addNew) {
                if (newFcName.Length > 0 && !Config.FcCustomizations.ContainsKey(newFcName)) {
                    Config.FcCustomizations.Add(newFcName, new TagCustomization());
                    newFcName = string.Empty;
                }
            }

            if (newFcName.Length == 0) {
                ImGui.SameLine();
                ImGui.TextDisabled(LocString("NoFCNote", "Enter name to add FC to list."));
            } else if (Config.FcCustomizations.ContainsKey(newFcName)) {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), LocString("FCAlreadyAddedError", "FC is already on list."));
            }

            ImGui.EndTable();

            if (ImGui.CollapsingHeader("Supported Icons")) {
                if (ImGui.BeginTable("iconViewer", 1 + (int)(ImGui.GetContentRegionAvail().X / 100))) {
                    foreach (var i in GraphicFont.FontIcons.Icons) {
                        if (i.IsValid()) {
                            ImGui.TableNextColumn();
                            i.Draw();
                            if (ImGui.IsItemHovered()) {
                                ImGui.BeginTooltip();
                                ImGui.Text($"<icon:{i.ID}>");
                                ImGui.Separator();
                                i.DrawScaled(new Vector2(2));
                                ImGui.EndTooltip();
                            }

                            if (ImGui.IsItemClicked()) {
                                ImGui.SetClipboardText($"<icon:{i.ID}>");
                            }
                        }
                    }

                    ImGui.EndTable();
                }
            }
        }
    }

    private enum ChangeType {
        None,
        Delete,
        Rename,
    }

    private ChangeType TagCustomizationEditor(ref string name, TagCustomization tc, bool canChange = true) {
        ImGui.TableNextColumn();

        var changeType = ChangeType.None;

        if (canChange) {
            if (ImGui.Button($"X##fcList#{GetType().Name}_delete_{name}", new Vector2(-1, 24 * ImGui.GetIO().FontGlobalScale))) {
                changeType = ChangeType.Delete;
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Remove {name}");
        }

        ImGui.TableNextColumn();
        ImGui.Checkbox($"##fcList#{GetType().Name}_enable_{name}", ref tc.Enabled);

        ImGui.TableNextColumn();

        ImGui.SetNextItemWidth(-1);
        var isEditingName = false;
        if (canChange) {
            if (ImGui.InputText($"##fcList#{GetType().Name}_name_{name}", ref name, 5, ImGuiInputTextFlags.EnterReturnsTrue)) {
                changeType = ChangeType.Rename;
            }

            if (ImGui.IsItemEdited()) {
                isEditingName = true;
            }
        } else {
            var s = string.Empty;
            ImGui.InputTextWithHint($"##fcList#{GetType().Name}_name_{name}", name, ref s, 5, ImGuiInputTextFlags.ReadOnly);
        }

        ImGui.TableNextColumn();
        if (isEditingName) {
            if (Config.FcCustomizations.ContainsKey(name)) {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), LocString("DuplicateNameError", "This name is already added."));
            } else {
                ImGui.TextDisabled(LocString("SaveMessage", "Press ENTER to save FC Tag."));
            }
        } else {
            ImGui.SetNextItemWidth(-180 * ImGui.GetIO().FontGlobalScale);

            if (tc.Enabled) {
                ImGui.InputTextWithHint($"##fcList#{GetType().Name}_replacement_{name}", "Hidden", ref tc.Replacement, 200);
                if (ImGui.IsItemFocused() && ImGui.IsItemActive()) {
                    ImGui.SetNextWindowPos(new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetItemRectSize().X - placeholderTooltipSize.X, ImGui.GetCursorScreenPos().Y));
                    ImGui.BeginTooltip();
                    if (ImGui.BeginTable("placeholdersTable", 2, ImGuiTableFlags.Borders)) {
                        ImGui.TableSetupColumn("Placeholders", ImGuiTableColumnFlags.WidthFixed, 190 * ImGui.GetIO().FontGlobalScale);
                        ImGui.TableSetupColumn("Description");

                        ImGui.TableHeadersRow();

                        ImGui.TableNextColumn();
                        ImGui.Text("<i> & </i>");
                        ImGui.TableNextColumn();
                        ImGui.Text("Begin and end Italics.");
                        ImGui.TableNextColumn();
                        ImGui.Text("<fctag>");
                        ImGui.TableNextColumn();
                        ImGui.Text("The character's existing FC tag.");

                        ImGui.TableNextColumn();
                        ImGui.Text("<level>");
                        ImGui.TableNextColumn();
                        ImGui.Text("The character's current job level.");

                        ImGui.TableNextColumn();
                        ImGui.Text("<homeworld>");
                        ImGui.TableNextColumn();
                        ImGui.Text("The name of the character's homeworld.");

                        ImGui.TableNextColumn();
                        ImGui.Text("<crossworldicon>");
                        ImGui.TableNextColumn();
                        ImGui.Text($"The {(char)SeIconChar.CrossWorld} icon.");
                        ImGui.TableNextColumn();
                        ImGui.Text("<colour:#abc123> & </colour>\n<glow:#abc123> & </glow>");
                        ImGui.TableNextColumn();
                        ImGui.Text($"Change the colour of the tag with full RGB support.\nReplace 'abc123' with any hex colour code.");
                        ImGui.TableNextColumn();
                        ImGui.Text("<icon:#>");
                        ImGui.TableNextColumn();
                        ImGui.Text($"A supported icon.\nReplace # with the icon number.");
                        ImGui.EndTable();
                    }

                    placeholderTooltipSize = ImGui.GetWindowSize();
                    ImGui.EndTooltip();
                }
            } else {
                var s = string.Empty;
                ImGui.InputTextWithHint($"##fcList#{GetType().Name}_replacement_{name}", "Unchanged", ref s, 50, ImGuiInputTextFlags.ReadOnly);
            }

            ImGui.SameLine();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - 180 * ImGui.GetIO().FontGlobalScale) + ImGui.GetStyle().ItemSpacing.X);
            ImGui.Checkbox($"Own Line##{GetType().Name}_ownLine_{name}", ref tc.OwnLine);
            ImGui.SameLine();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - 80 * ImGui.GetIO().FontGlobalScale) + ImGui.GetStyle().ItemSpacing.X);
            ImGui.Checkbox($"Hide «»##{GetType().Name}_hideQuotes_{name}", ref tc.HideQuoteMarks);
        }

        return changeType;
    }

    private Vector2 placeholderTooltipSize = new(0);
}
