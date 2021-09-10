using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientInterface.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class CustomFreeCompanyTags : UiAdjustments.SubTweak {
        public class Configs : TweakConfig {
            public Dictionary<string, TagCustomization> FcCustomizations = new();
            public TagCustomization DefaultCustomization = new();
            public TagCustomization WandererCustomization = new() { Enabled = false, Replacement = "<crossworldicon><homeworld>"};
        }

        public class TagCustomization {
            public bool Enabled;
            public string Replacement = string.Empty;
        }

        private Configs config;

        private delegate void* UpdateNameplateDelegate(RaptureAtkModuleStruct* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex);
        private HookWrapper<UpdateNameplateDelegate> updateNameplateHook;
        
        public override string Name => "Custom Free Company Tags";
        public override string Description => "Allows hiding or customizing Free Company and Wanderer tags.";
        
        public override void Enable() {
            if (Enabled) return;
            config = LoadConfig<Configs>() ?? new Configs();
            updateNameplateHook ??= Common.Hook<UpdateNameplateDelegate>("40 53 55 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", UpdateNameplatesDetour);
            updateNameplateHook?.Enable();
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(config);
            updateNameplateHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            updateNameplateHook?.Dispose();
            base.Dispose();
        }

        private void* UpdateNameplatesDetour(RaptureAtkModuleStruct* raptureAtkModule, NamePlateInfo* namePlateInfo, NumberArrayData* numArray, StringArrayData* stringArray, GameObject* gameObject, int numArrayIndex, int stringArrayIndex) {
            if (gameObject->ObjectKind != 1) goto ReturnOriginal;
            var battleChara = (BattleChara*) gameObject;
            try {
                var customization = config.DefaultCustomization;
                string companyTag = string.Empty;
                if (battleChara->Character.HomeWorld != battleChara->Character.CurrentWorld) {
                    // Wanderer
                    customization = config.WandererCustomization;
                } else {
                    companyTag = Encoding.UTF8.GetString(battleChara->Character.FreeCompanyTag, 6).Trim('\0', ' ');

                    customization = companyTag.Length switch {
                        <= 0 => null,
                        > 0 when config.FcCustomizations.ContainsKey(companyTag) => config.FcCustomizations[companyTag],
                        _ => customization
                    };
                }
                
                
                

                if (customization != null && customization.Enabled) {
                    if (customization.Replacement.Trim().Length == 0) {
                        namePlateInfo->FcName.StringPtr[0] = 0;
                    } else {
                        var payloads = new List<Payload> {
                            new TextPayload(" «")
                        };

                        var cText = string.Empty;

                        var resetForeground = false;
                        var resetGlow = false;
                        

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
                                            var world = External.Data.Excel.GetSheet<World>().GetRow(battleChara->Character.HomeWorld);
                                            payloads.Add(new TextPayload(world.Name));
                                            break;
                                        }
                                        case "<fctag>": {
                                            payloads.Add(new TextPayload(companyTag));
                                            break;
                                        }
    #if DEBUG
                                        case "<flags>": {
                                            payloads.Add(new TextPayload($"{namePlateInfo->Flags:X}"));
                                            break;
                                        }
#endif
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
        private string newFcName = string.Empty;
        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {

            if (ImGui.BeginTable("fcList#noFreeCompanyOnNamePlate", 4)) {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn($"Replace", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("FC Tag", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 75* ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Replacement", ImGuiTableColumnFlags.NoClip);
                ImGui.TableHeadersRow();


                string deleteKey = null;
                string renameKey = null;
                
                foreach (var fc in config.FcCustomizations.OrderBy(k => k.Key)) {
                    var k = fc.Key;
                    var edit = TagCustomizationEditor(ref k, fc.Value);
                    if (edit == ChangeType.Rename) {
                        if (!config.FcCustomizations.ContainsKey(k)) {
                            deleteKey = fc.Key;
                            renameKey = k;
                        }
                    } else if (edit == ChangeType.Delete) {
                        deleteKey = fc.Key;
                    }
                }

                if (!string.IsNullOrEmpty(deleteKey)) {
                    if (!string.IsNullOrEmpty(renameKey)) {
                        config.FcCustomizations.Add(renameKey, config.FcCustomizations[deleteKey]);
                    } 
                    config.FcCustomizations.Remove(deleteKey);
                }
                
                TagCustomizationEditor(ref wandererString, config.WandererCustomization, false);
                TagCustomizationEditor(ref defaultString, config.DefaultCustomization, false);
                // TagCustomizationEditor(ref noCompanyString, config.NoCompanyCustomization, false);
                
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Add FC:");
                
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var addNew = ImGui.InputText($"##fcList#{GetType().Name}_new_name", ref newFcName, 5, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.TableNextColumn();
                if (ImGui.Button($"Add##fcList#{GetType().Name}_new_button") || addNew) {
                    if (newFcName.Length > 0 && !config.FcCustomizations.ContainsKey(newFcName)) {
                        config.FcCustomizations.Add(newFcName, new TagCustomization());
                        newFcName = string.Empty;
                    }
                }

                if (newFcName.Length == 0) {
                    ImGui.SameLine();
                    ImGui.TextDisabled("Enter name to add FC to list.");
                } else if (config.FcCustomizations.ContainsKey(newFcName)) {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "FC is already on list.");
                }


                ImGui.EndTable();
            }
        };

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
                if (config.FcCustomizations.ContainsKey(name)) {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "This name is already added.");
                } else {
                    ImGui.TextDisabled("Press ENTER to save FC Tag.");
                }
                
            } else {
                ImGui.SetNextItemWidth(-1);
            
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
                            ImGui.Text("<fctag>");
                            ImGui.TableNextColumn();
                            ImGui.Text("The character's existing FC tag.");
                            
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
                            ImGui.EndTable();
                        }

                        placeholderTooltipSize = ImGui.GetWindowSize();
                        ImGui.EndTooltip();
                    }
                } else {
                    var s = string.Empty;
                    ImGui.InputTextWithHint($"##fcList#{GetType().Name}_replacement_{name}", "Unchanged", ref s, 50, ImGuiInputTextFlags.ReadOnly);
                }
            }
            
            return changeType;
        }

        private Vector2 placeholderTooltipSize = new(0);
    }
}
