using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule;
using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class CustomFreeCompanyTags : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public Dictionary<string, TagCustomization> FcCustomizations = new();
        public TagCustomization DefaultCustomization = new();
        public TagCustomization WandererCustomization = new() { Enabled = false, Replacement = "<crossworldicon><homeworld>"};
        public TagCustomization TravellerCustomization;
    }

    public class TagCustomization {
        public bool Enabled;
        public string Replacement = string.Empty;
        public bool HideInDuty;
        public bool HideQuoteMarks;
        public bool OwnLine;
    }

    public Configs Config { get; private set; }

    private delegate void* UpdateNameplateDelegate(RaptureAtkModule* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex);
    private HookWrapper<UpdateNameplateDelegate> updateNameplateHook;
        
    public override string Name => "Custom Free Company Tags";
    public override string Description => "Allows hiding or customizing Free Company and Wanderer tags.";

    public override void Setup() {
        AddChangelog(Changelog.UnreleasedVersion, "Added option to display FC tags on a separate line to character name.");
        base.Setup();
    }

    public override void Enable() {
        if (Enabled) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        Config.TravellerCustomization ??= new TagCustomization() { Enabled = Config.WandererCustomization.Enabled, Replacement = Config.WandererCustomization.Replacement };
        updateNameplateHook ??= Common.Hook<UpdateNameplateDelegate>("40 53 55 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", UpdateNameplatesDetour);
        updateNameplateHook?.Enable();
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        updateNameplateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        updateNameplateHook?.Dispose();
        base.Dispose();
    }

    private void* UpdateNameplatesDetour(RaptureAtkModule* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex) {
        if (gameObject->ObjectKind != 1) goto ReturnOriginal;
        var battleChara = (BattleChara*) gameObject;
        try {
            var customization = Config.DefaultCustomization;
            string companyTag = string.Empty;
            if (battleChara->Character.HomeWorld != battleChara->Character.CurrentWorld) {
                // Wanderer
                var w = Service.Data.Excel.GetSheet<World>()?.GetRow(battleChara->Character.HomeWorld);
                if (w == null || w.DataCenter.Row == Service.ClientState.LocalPlayer?.CurrentWorld?.GameData?.DataCenter?.Row) {
                    customization = Config.WandererCustomization;
                } else {
                    customization = Config.TravellerCustomization;
                }
            } else {
                companyTag = Encoding.UTF8.GetString(battleChara->Character.FreeCompanyTag, 6).Trim('\0', ' ');

                customization = companyTag.Length switch {
                    <= 0 => null,
                    > 0 when Config.FcCustomizations.ContainsKey(companyTag) => Config.FcCustomizations[companyTag],
                    _ => customization
                };
            }
                
                
                

            if (customization != null && customization.Enabled) {
                if (customization.HideInDuty && Service.Condition[ConditionFlag.BoundByDuty56]) {
                    customization = new TagCustomization() { Enabled = true, Replacement = "" };
                }
                
                if (customization.Replacement.Trim().Length == 0) {
                    namePlateInfo->FcName.StringPtr[0] = 0;
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
                        

                    foreach (var t in customization.Replacement) {
                        switch (t) {
                            case '<': {
                                if (cText.Length > 0) {
                                    payloads.Add(new TextPayload(cText));
                                }

                                cText = "<";
                                break;
                            }
                            case '>' when cText.Length > 0 && cText[0] == '<' : {
                                cText += '>';
                                switch (cText.ToLower()) {
                                    case "<crossworldicon>": {
                                        payloads.Add(new IconPayload(BitmapFontIcon.CrossWorld));
                                        break;
                                    }
                                    case "<homeworld>": {
                                        var world = Service.Data.Excel.GetSheet<World>().GetRow(battleChara->Character.HomeWorld);
                                        
                                        payloads.Add(new TextPayload(world?.Name ?? $"UnknownWorld#{battleChara->Character.HomeWorld}"));
                                        break;
                                    }
                                    case "<level>": {
                                        payloads.Add(new TextPayload(battleChara->Character.Level.ToString()));
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
                                    case { } s when s.StartsWith("<color:"): {
                                        var k = s.Substring(7, s.Length - 8);
                                        if (ushort.TryParse(k, out var colorKey)) {
                                            payloads.Add(new UIForegroundPayload(colorKey));
                                            resetForeground = colorKey != 0;
                                        } else {
                                            payloads.Add(new TextPayload(cText));
                                        }
                                        break;
                                    }
                                    case { } s when s.StartsWith("<colour:"): {
                                        var k = s.Substring(8, s.Length - 9);
                                        if (ushort.TryParse(k, out var colorKey)) {
                                            payloads.Add(new UIForegroundPayload(colorKey));
                                            resetForeground = colorKey != 0;
                                        } else {
                                            payloads.Add(new TextPayload(cText));
                                        }
                                        break;
                                    }
                                    case { } s when s.StartsWith("<glow:"): {
                                        var k = s.Substring(6, s.Length - 7);
                                        if (ushort.TryParse(k, out var colorKey)) {
                                            payloads.Add(new UIGlowPayload(colorKey));
                                            resetGlow = colorKey != 0;
                                        } else {
                                            payloads.Add(new TextPayload(cText));
                                        }
                                        break;
                                    }
                                    case { } s when s.StartsWith("<icon:"): {
                                        var k = s.Substring(6, s.Length - 7);
                                        if (uint.TryParse(k, out var iconKey)) {
                                            payloads.Add(new IconPayload((BitmapFontIcon) iconKey));
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
                        
                    if (!customization.HideQuoteMarks) 
                        payloads.Add(new TextPayload("»"));
                    namePlateInfo->FcName.SetSeString(new SeString(payloads));
                }
            }
        } catch (Exception ex) {
            if (!errored) {
                errored = true;
                Plugin.Error(this, ex, true);
            }
        }

        ReturnOriginal:
        var original = updateNameplateHook.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, gameObject, numArrayIndex, stringArrayIndex);
        return original;
    }

    private bool errored;
        
    private string defaultString = "Default (FC)";
    private string wandererString = "Wanderer";
    private string travellerString = "Traveller";
    private string newFcName = string.Empty;
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

        if (ImGui.BeginTable("fcList#noFreeCompanyOnNamePlate", 4)) {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("Replace"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
            ImGui.TableSetupColumn(LocString("FC Tag"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 75* ImGui.GetIO().FontGlobalScale);
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
            TagCustomizationEditor(ref travellerString, Config.TravellerCustomization, false, true);
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

            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0, colourLinkHovered ? 1f : 0.5f, 0.5f, 1), LocString("ColourHelpLink", "Click here for a list of supported icons, colours, and glows."));
            if (ImGui.IsItemClicked()) Common.OpenBrowser("https://raw.githubusercontent.com/Caraxi/SimpleTweaksPlugin/main/images/placeholderHelp.png");
            if (colourLinkHovered = ImGui.IsItemHovered()) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            ImGui.EndTable();
        }
    };

    private bool colourLinkHovered = false;

    private enum ChangeType {
        None,
        Delete,
        Rename,
    }
        
    private ChangeType TagCustomizationEditor(ref string name, TagCustomization tc, bool canChange = true, bool showHideInDuty = false) {
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
            ImGui.SetNextItemWidth((showHideInDuty ? -290 : -180) * ImGui.GetIO().FontGlobalScale);
            
            if (tc.Enabled) {
                ImGui.InputTextWithHint($"##fcList#{GetType().Name}_replacement_{name}", "Hidden", ref tc.Replacement, 200);
                if (ImGui.IsItemFocused() && ImGui.IsItemActive()) {
                        
                    ImGui.SetNextWindowPos(new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetItemRectSize().X - placeholderTooltipSize.X, ImGui.GetCursorScreenPos().Y));
                    ImGui.BeginTooltip();
                    if (ImGui.BeginTable("placeholdersTable", 2, ImGuiTableFlags.Borders)) {
                        ImGui.TableSetupColumn("Placeholders", ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
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
                        ImGui.Text("<colour:#>\n<glow:#>");
                        ImGui.TableNextColumn();
                        ImGui.Text($"Change the colour of the tag.\nReplace # with a colour number.");
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
            
            if (showHideInDuty) {
                ImGui.SameLine();
                ImGui.Checkbox($"Hide in Duty##{GetType().Name}_hideInDuty_{name}", ref tc.HideInDuty);
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