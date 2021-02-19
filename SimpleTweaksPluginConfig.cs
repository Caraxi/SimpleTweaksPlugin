using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private SimpleTweaksPlugin plugin;

        public int Version { get; set; } = 2;

        public SimpleTweaksPluginConfig() { }

        public List<string> EnabledTweaks = new List<string>();

        public bool HideKofi;
        public bool ShowExperimentalTweaks;

        public void Init(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }

        [NonSerialized] private SubTweakManager setTab = null;
        [NonSerialized] private bool settingTab = false;
        [NonSerialized] private string searchInput = string.Empty;
        [NonSerialized] private string lastSearchInput = string.Empty;
        [NonSerialized] private List<BaseTweak> searchResults = new List<BaseTweak>();
        
        public bool DrawConfigUI() {
            var drawConfig = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            var windowFlags = ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(new Vector2(600 * scale, 200 * scale), new Vector2(800 * scale, 800 * scale));
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);

            if (plugin.ErrorList.Count != 0) {
                ImGui.PushStyleColor(ImGuiCol.Button, 0x990000FF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x880000FF);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA0000FF);
                var c = ImGui.GetCursorPos();
                var buttonText = $"{plugin.ErrorList.Count} Errors Detected";
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize(buttonText).X);
                if (ImGui.Button(buttonText)) {
                    plugin.ShowErrorWindow = true;
                }
                ImGui.SetCursorPos(c);
                ImGui.PopStyleColor(3);
            } else if (!HideKofi) {
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF5E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF5E5BAA);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5E5BDD);
                var c = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize("Support on Ko-fi").X);
                if (ImGui.SmallButton("Support on Ko-fi")) {
                    Process.Start("https://ko-fi.com/Caraxi");
                }
                ImGui.SetCursorPos(c);
                ImGui.PopStyleColor(3);
            }

            ImGui.Text("Enable or disable any tweaks here.\nAll tweaks are disabled by default.");
            
            ImGui.Separator();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("###tweakSearchInput", "Search...", ref searchInput, 100);
            ImGui.Separator();

            if (!string.IsNullOrEmpty(searchInput)) {
                if (lastSearchInput != searchInput) {
                    lastSearchInput = searchInput;
                    searchResults = new List<BaseTweak>();
                    var searchValue = searchInput.ToLowerInvariant();
                    foreach (var t in plugin.Tweaks) {
                        if (t is SubTweakManager stm) {
                            foreach (var st in stm.GetTweakList()) {
                                if (st.Name.ToLowerInvariant().Contains(searchValue)) {
                                    searchResults.Add(st);
                                }
                            }
                            continue;
                        }
                        if (t.Name.ToLowerInvariant().Contains(searchValue)) {
                            searchResults.Add(t);
                        }
                    }
                }

                foreach (var t in searchResults) {
                    var enabled = t.Enabled;
                    if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                    if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                        if (enabled) {
                            SimpleLog.Debug($"Enable: {t.Name}");
                            try {
                                t.Enable();
                                if (t.Enabled) {
                                    EnabledTweaks.Add(t.GetType().Name);
                                }
                            } catch (Exception ex) {
                                plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                            }
                        } else {
                            SimpleLog.Debug($"Disable: {t.Name}");
                            try {
                                t.Disable();
                            } catch (Exception ex) {
                                plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                            }
                            EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                        }
                        Save();
                    }
                    ImGui.SameLine();
                    t.DrawConfig(ref changed);
                    ImGui.Separator();
                }
            } else {
                var flags = settingTab ? ImGuiTabBarFlags.AutoSelectNewTabs : ImGuiTabBarFlags.None;
                if (ImGui.BeginTabBar("tweakCategoryTabBar", flags)) {
                    if (settingTab && setTab == null) {
                        settingTab = false;
                    } else {
                        if (ImGui.BeginTabItem("General Tweaks")) {
                            ImGui.BeginChild("generalTweaks", new Vector2(-1, -1), false);
                            foreach (var t in plugin.Tweaks.Where(t => t is SubTweakManager).Cast<SubTweakManager>()) {
                                var enabled = t.Enabled;
                                if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                                if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                                    if (enabled) {
                                        SimpleLog.Debug($"Enable: {t.Name}");
                                        try {
                                            t.Enable();
                                            if (t.Enabled) {
                                                EnabledTweaks.Add(t.GetType().Name);
                                            }
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                                        }
                                    } else {
                                        SimpleLog.Debug($"Disable: {t.Name}");
                                        try {
                                            t.Disable();
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                                        }
                                        EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                                    }
                                    Save();
                                }
                                ImGui.SameLine();
                                ImGui.TreeNodeEx($"Category: {t.Name}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                                if (ImGui.IsItemClicked() && t.Enabled) {
                                    setTab = t;
                                    settingTab = false;
                                }
                                ImGui.Separator();
                            }
                            // ImGui.Separator();
                            foreach (var t in plugin.Tweaks) {
                                if (t is SubTweakManager) continue;
                                var enabled = t.Enabled;
                                if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                                if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                                    if (enabled) {
                                        SimpleLog.Debug($"Enable: {t.Name}");
                                        try {
                                            t.Enable();
                                            if (t.Enabled) {
                                                EnabledTweaks.Add(t.GetType().Name);
                                            }
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                                        }
                                    } else {
                                        SimpleLog.Debug($"Disable: {t.Name}");
                                        try {
                                            t.Disable();
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                                        }
                                        EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                                    }
                                    Save();
                                }
                                ImGui.SameLine();
                                t.DrawConfig(ref changed);
                                ImGui.Separator();
                            }
                            
                            ImGui.EndChild();
                            ImGui.EndTabItem();
                        }
                    }
                    
                    foreach (var stm in plugin.Tweaks.Where(t => t is SubTweakManager && t.Enabled)) {
                        if (settingTab == false && setTab == stm) {
                            settingTab = true;
                            continue;
                        }

                        if (settingTab && setTab == stm) {
                            settingTab = false;
                            setTab = null;
                        }
                        
                        if (ImGui.BeginTabItem($"{stm.Name}##tweakCategoryTab")) {
                            ImGui.BeginChild($"{stm.Name}-scroll", new Vector2(-1, -1));
                            stm.DrawHeaderlessConfig(ref changed);
                            ImGui.EndChild();
                            ImGui.EndTabItem();
                        }
                    }

                    if (ImGui.BeginTabItem("General Options")) {
                        ImGui.BeginChild($"generalOptions-scroll", new Vector2(-1, -1));
                        ImGui.BeginGroup();
                        if (ImGui.Checkbox("Show Experimental Tweaks.", ref ShowExperimentalTweaks)) Save();
                        if (ImGui.Checkbox("Hide Ko-fi link.", ref HideKofi)) Save();
                        ImGui.EndGroup();
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    
                    ImGui.EndTabBar();
                }
            }
            
            ImGui.End();

            if (changed) {
                Save();
            }
            
            return drawConfig;
        }
    }
}