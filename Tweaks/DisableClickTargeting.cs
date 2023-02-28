﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public bool ShouldSerializeDisableClickTargeting() => false;
        public DisableClickTargeting.Configs DisableClickTargeting;
    }
}

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class DisableClickTargeting : Tweak {

        public class NameFilter {
            public string Name = string.Empty;
            public bool DisableLeft;
            public bool DisableRight;
            public bool OnlyInCombat;
        }

        public class Configs : TweakConfig {
            public bool DisableRightClick = true;
            public bool DisableLeftClick;
            public bool OnlyDisableInCombat;
            public bool UseNameFilter;
            public List<NameFilter> NameFilters = new();
        }

        public Configs Config { get; private set; }

        private string nameFilterNew = string.Empty;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {

            if (!Config.UseNameFilter) {
                hasChanged |= ImGui.Checkbox(LocString("SimpleDisableRightClick","Disable Right Click Targeting"), ref Config.DisableRightClick);
                hasChanged |= ImGui.Checkbox(LocString("SimpleDisableLeftClick","Disable Left Click Targeting"), ref Config.DisableLeftClick);

                ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.Checkbox(LocString("SimpleCombatOnly", "Only disable in combat"), ref Config.OnlyDisableInCombat);
                ImGui.Dummy(new Vector2(10) * ImGui.GetIO().FontGlobalScale);
            }
           
            hasChanged |= ImGui.Checkbox(LocString("NameFiltering", "Enable Name Filtering"), ref Config.UseNameFilter);

            if (!(Config.DisableLeftClick || Config.DisableRightClick || Config.UseNameFilter)) {
                ImGui.Text(LocString("EverythingDisabled", "It is doing nothing if everything is disabled..."));
            }

            if (Config.UseNameFilter) {
                
                ImGui.Text(LocString("NameFiltersLabel", "Name Filters:"));
                ImGui.SameLine();
                ImGui.TextDisabled(LocString("NameFiltersHelp", "Per actor options for "));
                var i = 0;


                if (ImGui.BeginTable("nameFilterTable", 5)) {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn(LocString("NameHeader", "\nName"));
                    ImGui.TableSetupColumn(LocString("LeftHeader", "Disable\nLeft"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn(LocString("RightHeader", "Disable\nRight"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn(LocString("CombatHeader", "Only in\nCombat"), ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoClip, 50 * ImGui.GetIO().FontGlobalScale);
                    
                    ImGui.TableHeadersRow();
                    NameFilter deleteNf = null;
                    foreach (var nf in Config.NameFilters) {
                        ImGui.TableNextColumn();
                        if (ImGui.Button($"X##namefilter_delete_{++i}", new Vector2(-1, 24 * ImGui.GetIO().FontGlobalScale))) {
                            deleteNf = nf;
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(LocString("RemoveTooltip", "Remove {0}").Format(nf.Name));
                        ImGui.TableNextColumn();
                        ImGui.Text(nf.Name);
                        ImGui.TableNextColumn();
                        hasChanged |= ImGui.Checkbox($"##nameFilter_disableLeft{i}", ref nf.DisableLeft);
                        ImGui.TableNextColumn();
                        hasChanged |= ImGui.Checkbox($"##nameFilter_disableRight{i}", ref nf.DisableRight);
                        ImGui.TableNextColumn();
                        hasChanged |= ImGui.Checkbox($"##nameFilter_onlyCombat{i}", ref nf.OnlyInCombat);
                    }

                    if (deleteNf != null) {
                        Config.NameFilters.Remove(deleteNf);
                        hasChanged = true;
                    }
                    
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextWithHint($"##nameFilter_name{++i}", LocString("NamePlaceholder", "Name"), ref nameFilterNew, 30);
                    
                    ImGui.TableNextColumn();
                    if (ImGui.Button(LocString("AddButton", "Add"))) {
                        if (Config.NameFilters.All(nf => nf.Name != nameFilterNew)) {
                            Config.NameFilters.Add(new NameFilter() {
                                Name = nameFilterNew
                            });
                            hasChanged = true;
                        }
                        nameFilterNew = string.Empty;
                    }
                    ImGui.TableNextColumn();
                    var target = Service.Targets.SoftTarget ?? Service.Targets.Target;
                    if (target != null) {
                        if (ImGui.Button("Target")) {
                            nameFilterNew = target.Name.TextValue;
                        }
                    }
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text(LocString("DefaultNameText", "Default (Unmatched Names)"));
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_disableLeft{i}", ref Config.DisableLeftClick);
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_disableRight{i}", ref Config.DisableRightClick);
                    ImGui.TableNextColumn();
                    hasChanged |= ImGui.Checkbox($"##nameFilter_onlyCombat{i}", ref Config.OnlyDisableInCombat);
                    
                    ImGui.EndTable();
                }
            } 
            
            if (hasChanged && Enabled) {
                Disable();
                Enable();
            }
        };

        public override string Name => "Disable Click Targeting";
        public override string Description => "Allows disabling of the target function on left and right mouse clicks.";

        private delegate void* ClickTarget(void** a1, byte* a2, bool a3);
        private HookWrapper<ClickTarget> rightClickTargetHook;
        private HookWrapper<ClickTarget> leftClickTargetHook;
        
        public override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.DisableClickTargeting ?? new Configs();
            
            rightClickTargetHook ??= Common.Hook(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B"), new ClickTarget(RightClickTargetDetour));
            leftClickTargetHook ??= Common.Hook(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 16"), new ClickTarget(LeftClickTargetDetour));
            if (Config.DisableRightClick || Config.UseNameFilter) rightClickTargetHook?.Enable();
            if (Config.DisableLeftClick || Config.UseNameFilter) leftClickTargetHook?.Enable();
            base.Enable();
        }
        
        public override void Disable() {
            SaveConfig(Config);
            PluginConfig.DisableClickTargeting = null;
            rightClickTargetHook?.Disable();
            leftClickTargetHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            rightClickTargetHook?.Dispose();
            leftClickTargetHook?.Dispose();
            base.Dispose();
        }

        private void* RightClickTargetDetour(void** a1, byte* a2, bool a3) {
            if (a2 != null && a2 == a1[16]) return rightClickTargetHook.Original(a1, a2, a3);
            
            if (a2 != null && Config.UseNameFilter) {
                int l;
                for (l = 0; l < 60; l++) {
                    if (a2[0x30 + l] == 0) break;
                }

                if (l > 0) {
                    var actorName = Encoding.UTF8.GetString(a2 + 0x30, l).Trim();
                    var nf = Config.NameFilters.FirstOrDefault(a => a.Name == actorName);
                    if (nf != default) {
                        if ((nf.OnlyInCombat && !Service.Condition[ConditionFlag.InCombat]) || (!nf.DisableRight)) return rightClickTargetHook.Original(a1, a2, a3);
                        return null;
                    }
                }
            }
            
            if (!Config.DisableRightClick || (Config.OnlyDisableInCombat && !Service.Condition[ConditionFlag.InCombat])) {
                return rightClickTargetHook.Original(a1, a2, a3);
            }
            return null;
        }
        
        private void* LeftClickTargetDetour(void** a1, byte* a2, bool a3) {
            if (a2 != null && a2 == a1[16]) return leftClickTargetHook.Original(a1, a2, a3);
            
            if (a2 != null && Config.UseNameFilter) {
                int l;
                for (l = 0; l < 60; l++) {
                    if (a2[0x30 + l] == 0) break;
                }

                if (l > 0) {
                    var actorName = Encoding.UTF8.GetString(a2 + 0x30, l).Trim();
                    var nf = Config.NameFilters.FirstOrDefault(a => a.Name == actorName);
                    if (nf != default) {
                        if ((nf.OnlyInCombat && !Service.Condition[ConditionFlag.InCombat]) || (!nf.DisableLeft)) return leftClickTargetHook.Original(a1, a2, a3);
                        return null;
                    }
                }
            }
            
            if (!Config.DisableLeftClick || Config.OnlyDisableInCombat && !Service.Condition[ConditionFlag.InCombat]) {
                return leftClickTargetHook.Original(a1, a2, a3);
            }
            return null;
        }
    }
}
