using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin; 

public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
    [NonSerialized]
    private SimpleTweaksPlugin plugin;

    public int Version { get; set; } = 3;

    public SimpleTweaksPluginConfig() { }

    public List<string> EnabledTweaks = new List<string>();
    public List<string> HiddenTweaks = new List<string>();
    public List<string> CustomProviders = new List<string>();
    public List<string> BlacklistedTweaks = new List<string>();

    public bool HideKofi;
    public bool ShowExperimentalTweaks;
    public bool DisableAutoOpen;
    public bool ShowInDevMenu;
    public bool NoFools;
    public bool NotBaby;
    public bool AnalyticsOptOut;

    public bool ShowTweakDescriptions = true;
    public bool ShowTweakIDs;

    public string CustomCulture = string.Empty;
    public string Language = null;

    public string LastSeenChangelog = string.Empty;
    public bool AutoOpenChangelog = false;
    public bool DisableChangelogNotification = false;

    public void Init(SimpleTweaksPlugin plugin) {
        this.plugin = plugin;
        HiddenTweaks.RemoveAll(t => EnabledTweaks.Contains(t));
    }

    public void Save() {
        Service.PluginInterface.SavePluginConfig(this);
    }

    [NonSerialized] private SubTweakManager setTab = null;
    [NonSerialized] private bool settingTab = false;
    [NonSerialized] private string searchInput = string.Empty;
    [NonSerialized] private string lastSearchInput = string.Empty;
    [NonSerialized] private List<BaseTweak> searchResults = new List<BaseTweak>();

    internal void FocusTweak(BaseTweak tweak) {
        plugin.ConfigWindow.IsOpen = true;
        plugin.ConfigWindow.Collapsed = false;
        searchResults.Clear();
        
        if (tweak is SubTweakManager stm) {
            setTab = stm;
            searchInput = string.Empty;
            lastSearchInput = string.Empty;
            return;
        }
        
        searchInput = tweak.Name;
        lastSearchInput = tweak.Name;
        searchResults.Add(tweak);
        tweak.ForceOpenConfig = true;
    }

    internal void ClearSearch() {
        searchInput = string.Empty;
        lastSearchInput = string.Empty;
        searchResults.Clear();
    }

    private string addCustomProviderInput = string.Empty;

    private Vector2 checkboxSize = new(16);
    private void DrawTweakConfig(BaseTweak t, ref bool hasChange) {
        var enabled = t.Enabled;
        if (t.Experimental && !ShowExperimentalTweaks && !enabled) return;

        if (t is IDisabledTweak || (!enabled && ImGui.GetIO().KeyShift) || t.TweakManager is {Enabled: false}) {
            if (HiddenTweaks.Contains(t.Key)) {
                if (ImGui.Button($"S##unhideTweak_{t.Key}", checkboxSize)) {
                    HiddenTweaks.Remove(t.Key);
                    Save();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(Loc.Localize("Unhide Tweak", "Unhide Tweak"));
                }
            } else {
                if (ImGui.Button($"H##hideTweak_{t.Key}", checkboxSize)) {
                    HiddenTweaks.Add(t.Key);
                    Save();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(Loc.Localize("Hide Tweak", "Hide Tweak"));
                }
            }
        } else
        if (ImGui.Checkbox($"###{t.Key}enabledCheckbox", ref enabled)) {
            if (enabled) {
                SimpleLog.Debug($"Enable: {t.Name}");
                try {
                    t.InternalEnable();
                    if (t.Enabled) {
                        EnabledTweaks.Add(t.Key);
                    }
                } catch (Exception ex) {
                    plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                }
            } else {
                SimpleLog.Debug($"Disable: {t.Name}");
                try {
                    t.InternalDisable();
                } catch (Exception ex) {
                    plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                }
                EnabledTweaks.RemoveAll(a => a == t.Key);
            }
            Save();
        }
        checkboxSize = ImGui.GetItemRectSize();
        ImGui.SameLine();
        var descriptionX = ImGui.GetCursorPosX();
        if (!t.DrawConfig(ref hasChange)) {
            if (t is IDisabledTweak dt) {
                if (!string.IsNullOrEmpty(dt.DisabledMessage)) {
                    ImGui.SetCursorPosX(descriptionX);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                    ImGui.TreeNodeEx(" ", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                    ImGui.TextWrapped($"{dt.DisabledMessage}");
                    ImGui.PopStyleColor();
                }
            } else
            if (ShowTweakDescriptions && !string.IsNullOrEmpty(t.Description)) {
                ImGui.SetCursorPosX(descriptionX);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                ImGui.TreeNodeEx(" ", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, 0xFF888888);
                var tweakDescription = t.LocString("Description", t.Description, "Tweak Description");
                ImGui.TextWrapped($"{tweakDescription}");
                ImGui.PopStyleColor();
            }
        }
        ImGui.Separator();
    }
        
    public void DrawConfigUI() {
        var changed = false;

        var showbutton = plugin.ErrorList.Count != 0 || Changelog.HasNewChangelog || !HideKofi;
        var buttonText = plugin.ErrorList.Count > 0 ? $"{plugin.ErrorList.Count} Errors Detected" : Changelog.HasNewChangelog ? "New Changelog Available" : "Support on Ko-fi";
        var buttonColor = (uint) (plugin.ErrorList.Count > 0 ? 0x000000FF : Changelog.HasNewChangelog ? 0x0011AA05 : 0x005E5BFF);
            
        if (showbutton) {
            ImGui.SetNextItemWidth(-(ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X));
        } else {
            ImGui.SetNextItemWidth(-1);
        }
            
        ImGui.InputTextWithHint("###tweakSearchInput", "Search...", ref searchInput, 100);

        if (showbutton) {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);

            if (ImGui.Button(buttonText, new Vector2(-1, ImGui.GetItemRectSize().Y))) {
                if (plugin.ErrorList.Count == 0 && !Changelog.HasNewChangelog) {
                    Common.OpenBrowser("https://ko-fi.com/Caraxi");
                } else if (Changelog.HasNewChangelog) {
                    plugin.ChangelogWindow.IsOpen = true;
                } else  {
                    plugin.ShowErrorWindow = true;
                }
            }
            ImGui.PopStyleColor(3);
        }
            
        ImGui.Dummy(new Vector2(1, ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().ItemSpacing.Y * 2));
        ImGui.Separator();

        if (!string.IsNullOrEmpty(searchInput)) {
            if (lastSearchInput != searchInput) {
                lastSearchInput = searchInput;
                searchResults = new List<BaseTweak>();
                var searchValue = searchInput.ToLowerInvariant();
                foreach (var t in plugin.Tweaks) {
                    if (t is SubTweakManager stm) {
                        if (!stm.Enabled) continue;
                        foreach (var st in stm.GetTweakList()) {
                            if (st.Name.ToLowerInvariant().Contains(searchValue) || st.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue)) || st.LocalizedName.ToLowerInvariant().Contains(searchValue)) {
                                searchResults.Add(st);
                            }
                        }
                        continue;
                    }
                    if (t.Name.ToLowerInvariant().Contains(searchValue) || t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue))|| t.LocalizedName.ToLowerInvariant().Contains(searchValue)) {
                        searchResults.Add(t);
                    }
                }
                    
                searchResults = searchResults.OrderBy(t => t.Name).ToList();
            }

            ImGui.BeginChild("search_scroll", new Vector2(-1));
                
            foreach (var t in searchResults) {
                if (HiddenTweaks.Contains(t.Key) && !t.Enabled) continue;
                var tweakConfigChanged = false;
                DrawTweakConfig(t, ref tweakConfigChanged);
                changed |= tweakConfigChanged;
            }
                
            ImGui.EndChild();
        } else {
            var flags = settingTab ? ImGuiTabBarFlags.AutoSelectNewTabs : ImGuiTabBarFlags.None;
            if (ImGui.BeginTabBar("tweakCategoryTabBar", flags)) {
                if (settingTab && setTab == null) {
                    settingTab = false;
                } else {
                    if (ImGui.BeginTabItem(Loc.Localize("General Tweaks", "General Tweaks", "General Tweaks Tab Header") + "###generalTweaksTab")) {
                        ImGui.BeginChild("generalTweaks", new Vector2(-1, -1), false);

                        // ImGui.Separator();
                        foreach (var t in plugin.Tweaks) {
                            if (t is SubTweakManager) continue;
                            if (HiddenTweaks.Contains(t.Key) && !t.Enabled) continue;
                            DrawTweakConfig(t, ref changed);
                        }

                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                foreach (var stm in plugin.Tweaks.Where(t => t is SubTweakManager stm && (t.Enabled || stm.AlwaysEnabled)).Cast<SubTweakManager>()) {

                    var subTweakList = stm.GetTweakList().Where(t => t.Enabled || !HiddenTweaks.Contains(t.Key)).ToList();
                    if (subTweakList.Count <= 0) {
                        continue;
                    }
                    if (settingTab == false && setTab == stm) {
                        settingTab = true;
                        continue;
                    }

                    if (settingTab && setTab == stm) {
                        settingTab = false;
                        setTab = null;
                    }

                    if (ImGui.BeginTabItem($"{stm.LocalizedName}###tweakCategoryTab_{stm.Key}")) {
                        ImGui.BeginChild($"{stm.Key}-scroll", new Vector2(-1, -1));
                        foreach (var tweak in subTweakList) {
                            if (!tweak.Enabled && HiddenTweaks.Contains(tweak.Key)) continue;
                            DrawTweakConfig(tweak, ref changed);
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                if (ImGui.BeginTabItem(Loc.Localize("General Options / TabHeader", "General Options") + $"###generalOptionsTab")) {
                    ImGui.BeginChild($"generalOptions-scroll", new Vector2(-1, -1));

                    if (ImGui.Checkbox(Loc.Localize("General Options / Analytics Opt Out", "Opt out of analytics"), ref AnalyticsOptOut)) Save();
                    ImGui.Indent();
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                    ImGui.TextWrapped("Note: The current version of simple tweaks does not collect any analytics regardless of this setting. This is here to preemptively opt out of any future analytics that may or may not be added to the plugin. All information that may be collected will be anonymous, but may include information such as the list of enabled tweaks and a subset of configured options within those tweaks.");
                    ImGui.PopStyleColor();
                    ImGui.Unindent();
                    ImGui.Separator();
                    if (ImGui.Checkbox(Loc.Localize("General Options / Show Experimental Tweaks", "Show Experimental Tweaks."), ref ShowExperimentalTweaks)) Save();
                    ImGui.Separator();
                    if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak Descriptions","Show tweak descriptions."), ref ShowTweakDescriptions)) Save();
                    ImGui.Separator();
                    if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak IDs", "Show tweak IDs."), ref ShowTweakIDs)) Save();
                    ImGui.Separator();
                    
                    if (ImGui.Checkbox(Loc.Localize("General Options / Auto Open Changelog", "Open New Changelogs Automatically"), ref AutoOpenChangelog)) Save();
                    ImGui.Separator();
                    if (ImGui.Checkbox(Loc.Localize("General Options / Disable Changelog Notice", "Disable Changelog Notifications"), ref DisableChangelogNotification)) Save();

                    ImGui.SameLine();
                    if (ImGui.Button("Open Changelog")) {
                        plugin.ChangelogWindow.IsOpen = true;
                    }
                    
                    ImGui.Separator();
                    #if DEBUG
                    if (ImGui.Checkbox("Disable Auto Open", ref DisableAutoOpen)) Save();
                    ImGui.Separator();
                    #else
                    if (ImGui.Checkbox("Show in Dev Menu", ref ShowInDevMenu)) Save();
                    ImGui.Separator();
                    #endif

                    if (Loc.DownloadError != null) {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), Loc.DownloadError.ToString());
                    }

                    if (Loc.LoadingTranslations) {
                        ImGui.Text("Downloading Translations...");
                    } else {
                        ImGui.SetNextItemWidth(130);
                        if (ImGui.BeginCombo(Loc.Localize("General Options / Language", "Language"), plugin.PluginConfig.Language)) {

                            if (ImGui.Selectable("en", Language == "en")) {
                                Language = "en";
                                plugin.SetupLocalization();
                                Save();
                            }

#if DEBUG
                            if (ImGui.Selectable("DEBUG", Language == "DEBUG")) {
                                Language = "DEBUG";
                                plugin.SetupLocalization();
                                Save();
                            }
#endif

                            var locDir = Service.PluginInterface.GetPluginLocDirectory();

                            var locFiles = Directory.GetDirectories(locDir);

                            foreach (var f in locFiles) {
                                var dir = new DirectoryInfo(f);
                                if (ImGui.Selectable($"{dir.Name}##LanguageSelection", Language == dir.Name)) {
                                    Language = dir.Name;
                                    plugin.SetupLocalization();
                                    Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.SameLine();

                        if (ImGui.SmallButton("Update Translations")) {
                            Loc.UpdateTranslations();
                        }

#if DEBUG
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Export Localizable")) {

                            // Auto fill dictionary with all Name/Description
                            foreach (var t in plugin.Tweaks) {
                                t.LocString("Name", t.Name, "Tweak Name");
                                if (t.Description != null) t.LocString("Description", t.Description, "Tweak Description");

                                if (t is SubTweakManager stm) {
                                    foreach (var st in stm.GetTweakList()) {
                                        st.LocString("Name", st.Name, "Tweak Name");
                                        if (st.Description != null) st.LocString("Description", st.Description, "Tweak Description");
                                    }
                                }
                            }

                            try {
                                ImGui.SetClipboardText(Loc.ExportLoadedDictionary());
                            } catch (Exception ex) {
                                SimpleLog.Error(ex);
                            }
                        }
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Import")) {
                            var json = ImGui.GetClipboardText();
                            Loc.ImportDictionary(json);
                        }
#endif
                    }

                    ImGui.Separator();

                    ImGui.SetNextItemWidth(130);
                    if (ImGui.BeginCombo(Loc.Localize("General Options / Formatting Culture", "Formatting Culture"), plugin.Culture.Name)) {

                        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
                        for (var i = 0; i < cultures.Length; i++) {
                            var c = cultures[i];
                            if (ImGui.Selectable($"{c.Name}", Equals(c, plugin.Culture))) {
                                CustomCulture = c.Name;
                                plugin.Culture = c;
                                Save();
                            }
                        }

                        ImGui.EndCombo();
                    }
                    ImGui.SameLine();
                    ImGui.TextDisabled("Changes number formatting, not all tweaks support this.");

                    ImGui.Separator();
                    if (ImGui.Checkbox(Loc.Localize("General Options / Hide KoFi", "Hide Ko-fi link."), ref HideKofi)) Save();
                    ImGui.Separator();

                    foreach (var t in plugin.Tweaks.Where(t => t is SubTweakManager).Cast<SubTweakManager>()) {
                        if (t.AlwaysEnabled) continue;
                        var enabled = t.Enabled;
                        if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                        if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                            if (enabled) {
                                SimpleLog.Debug($"Enable: {t.Name}");
                                try {
                                    t.InternalEnable();
                                    if (t.Enabled) {
                                        EnabledTweaks.Add(t.GetType().Name);
                                    }
                                } catch (Exception ex) {
                                    plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                                }
                            } else {
                                SimpleLog.Debug($"Disable: {t.Name}");
                                try {
                                    t.InternalDisable();
                                } catch (Exception ex) {
                                    plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                                }
                                EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                            }
                            Save();
                        }
                        ImGui.SameLine();
                        ImGui.TreeNodeEx($"Enable Category: {t.LocalizedName}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                        if (ImGui.IsItemClicked() && t.Enabled) {
                            setTab = t;
                            settingTab = false;
                        }
                        ImGui.Separator();
                    }

                    if (HiddenTweaks.Count > 0) {
                        if (ImGui.TreeNode($"Hidden Tweaks ({HiddenTweaks.Count})###hiddenTweaks")) {
                            string removeKey = null;
                            foreach (var hidden in HiddenTweaks) {
                                var tweak = plugin.GetTweakById(hidden);
                                if (tweak == null) continue;
                                if (tweak is IDisabledTweak) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                                if (ImGui.Button($"S##unhideTweak_{tweak.Key}", new Vector2(23) * ImGui.GetIO().FontGlobalScale)) {
                                    removeKey = hidden;
                                }
                                if (ImGui.IsItemHovered()) {
                                    ImGui.SetTooltip(Loc.Localize("Unhide Tweak", "Unhide Tweak"));
                                }

                                ImGui.SameLine();
                                ImGui.Text(tweak.LocalizedName);

                                if (tweak is IDisabledTweak) {
                                    ImGui.SameLine();
                                    ImGui.Text("[Disabled]");
                                    ImGui.PopStyleColor();
                                }
                            }

                            if (removeKey != null) {
                                HiddenTweaks.RemoveAll(t => t == removeKey);
                                Save();
                            }
                            ImGui.TreePop();
                        }
                        ImGui.Separator();
                    }

                    if (CustomProviders.Count > 0 || ShowExperimentalTweaks) {
                        ImGui.Text("Tweak Providers:");
                        string? deleteCustomProvider = null;
                        for (var i = 0; i < CustomProviders.Count; i++) {
                            if (ImGui.Button($"X##deleteCustomProvider_{i}")) {
                                deleteCustomProvider = CustomProviders[i];
                            }
                            ImGui.SameLine();
                            if (ImGui.Button($"R##reloadcustomProvider_{i}")) {
                                foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                    if (tp.IsDisposed) continue;
                                    if (tp is not CustomTweakProvider ctp) continue;
                                    if (ctp.AssemblyPath == CustomProviders[i]) {
                                        ctp.Dispose();
                                    }
                                }
                                plugin.LoadCustomProvider(CustomProviders[i]);
                                Loc.ClearCache();
                            }

                            ImGui.SameLine();
                            var enabled = !CustomProviders[i].StartsWith("!");
                            if (ImGui.Checkbox($"###customProvider_{i}", ref enabled)) {

                                foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                    if (tp.IsDisposed) continue;
                                    if (tp is not CustomTweakProvider ctp) continue;
                                    if (ctp.AssemblyPath == CustomProviders[i]) {
                                        ctp.Dispose();
                                    }
                                    DebugManager.Reload();
                                }
                                
                                if (enabled) {
                                    if (CustomProviders[i].StartsWith("!")) CustomProviders[i] = CustomProviders[i].TrimStart('!');
                                    plugin.LoadCustomProvider(CustomProviders[i]);
                                } else {
                                    if (!CustomProviders[i].StartsWith("!")) CustomProviders[i] = "!" + CustomProviders[i];
                                }
                                
                                Save();
                            }
                            ImGui.SameLine();
                            ImGui.Text(CustomProviders[i].TrimStart('!'));
                        }

                        if (deleteCustomProvider != null) {
                            CustomProviders.Remove(deleteCustomProvider);

                            foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                if (tp.IsDisposed) continue;
                                if (tp is not CustomTweakProvider ctp) continue;
                                if (ctp.AssemblyPath == deleteCustomProvider) {
                                    ctp.Dispose();
                                }
                            }
                            DebugManager.Reload();

                            Save();
                        }

                        if (ImGui.Button("+##addCustomProvider")) {
                            if (!string.IsNullOrWhiteSpace(addCustomProviderInput) && !CustomProviders.Contains(addCustomProviderInput)) {
                                CustomProviders.Add(addCustomProviderInput);
                                SimpleTweaksPlugin.Plugin.LoadCustomProvider(addCustomProviderInput);
                                addCustomProviderInput = string.Empty;
                                Save();
                            }
                        }

                        ImGui.SameLine();
                        ImGui.InputText("##addCustomProviderInput", ref addCustomProviderInput, 500);
                    }

                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                    
                ImGui.EndTabBar();
            }
        }
        if (changed) {
            Save();
        }
    }

    public void RefreshSearch() => lastSearchInput = string.Empty;
}