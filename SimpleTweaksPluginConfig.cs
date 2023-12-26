using Dalamud.Configuration;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin;

public enum DecorationType {
    Christmas,
    Easter,
    Valentines,
    Halloween,
    
    None = -3,
    Auto = -2,
    Random = -1,
}

public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
    [NonSerialized]
    private SimpleTweaksPlugin plugin;

    public int Version { get; set; } = 3;

    public List<string> EnabledTweaks = new();
    public List<string> HiddenTweaks = new();
    public List<string> CustomProviders;
    public bool ShouldSerializeCustomProviders() => CustomProviders != null;
    
    public List<CustomTweakProviderConfig> CustomTweakProviders = new();
    public List<string> BlacklistedTweaks = new();
    public List<string> HiddenCategories = new();
    

    public Dictionary<string, string> CustomizedCommands = new();
    public Dictionary<string, List<string>> DisabledCommandAlias = new();

    public bool HideKofi;
    public bool ShowExperimentalTweaks;
    public bool DisableAutoOpen;
    public bool ShowInDevMenu;
    public bool NoFools;
    public bool NotBaby;
    public bool AnalyticsOptOut;
    public bool ShowAllTweaksTab = true;
    public bool ShowEnabledTweaksTab = true;
    public bool ShowOtherTweaksTab = true;
    public bool NoCallerInLog;

    public bool ShowTweakDescriptions = true;
    public bool ShowTweakIDs;

    public string CustomCulture = string.Empty;
    public string Language;

    public string LastSeenChangelog = string.Empty;
    public bool AutoOpenChangelog;
    public bool DisableChangelogNotification;

    public string MetricsIdentifier;
    
    public DecorationType FestiveDecorationType = DecorationType.Auto;
    
    public void Init(SimpleTweaksPlugin plugin) {
        this.plugin = plugin;
        Update();
        HiddenTweaks.RemoveAll(t => EnabledTweaks.Contains(t));
    }

    private void Update() {
        if (CustomProviders != null) {
            foreach (var p in CustomProviders) {
                var enabled = !p.StartsWith("!");
                var path = enabled ? p : p.TrimStart('!');
                if (CustomTweakProviders.Any(ctp => ctp.Assembly.Equals(path, StringComparison.InvariantCultureIgnoreCase))) continue;
                var provider = new CustomTweakProviderConfig() {
                    Enabled = enabled,
                    Assembly = path,
                };
                
                CustomTweakProviders.Add(provider);
            }

            CustomProviders = null;
        }
    }
    
    public void Save() {
        Service.PluginInterface.SavePluginConfig(this);
    }
    
    [NonSerialized] private string searchInput = string.Empty;
    [NonSerialized] private string lastSearchInput = string.Empty;
    [NonSerialized] private List<BaseTweak> searchResults = new List<BaseTweak>();

    internal void FocusTweak(BaseTweak tweak) {
        if (tweak is SubTweakManager) return;
        plugin.ConfigWindow.IsOpen = true;
        plugin.ConfigWindow.Collapsed = false;
        searchResults.Clear();
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

    [NonSerialized] private string addCustomProviderInput = string.Empty;

    [NonSerialized] private Vector2 checkboxSize = new(16);

    private static string LocalizedCategoryName(string categoryName) => Loc.Localize($"Category / {categoryName}", categoryName, "Tweak Category");
    private static string LocalizedCategoryName(TweakCategory tweakCategory) => LocalizedCategoryName($"{tweakCategory}");

    private record TweakCategoryContainer(string CategoryName) {
        public string LocalizedName => LocalizedCategoryName(CategoryName);
        public List<BaseTweak> Tweaks = new();
        public virtual bool Equals(TweakCategoryContainer other) => CategoryName == other?.CategoryName;
        public override int GetHashCode() => CategoryName.GetHashCode();
    }

    [NonSerialized] private static List<TweakCategoryContainer> _tweakCategories;
    [NonSerialized] private static List<BaseTweak> _allTweaks;
    [NonSerialized] private static List<BaseTweak> _enabledTweaks;

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

    public static void RebuildTweakList() {
        _tweakCategories = null;
        _allTweaks = null;
    }

    private bool IsTweakVisible(BaseTweak tweak) {
        if (tweak.TweakManager is { Enabled: false }) return false;
        if (HiddenTweaks.Contains(tweak.Key) && !tweak.Enabled) return false;
        if (tweak.Experimental && !ShowExperimentalTweaks && !tweak.Enabled) return false;
        return true;
    }
    
    private void MixColour(Vector4 mix, params ImGuiCol[] cols) {
        foreach (var col in cols) {
            var current = ImGui.GetColorU32(col);
            var currentFloat = ImGui.ColorConvertU32ToFloat4(current);

            var newCol = Vector4.Zero + currentFloat;
            for (var i = 0; i < 4; i++) {
                if (mix[i] < 0) continue;
                newCol[i] += mix[i];
                newCol[i] /= 2;
            }

            ImGui.PushStyleColor(col, newCol);
        }
    }

    private void BuildTweakList() {
        var allTweaksList = new List<BaseTweak>();
        var uncategorizedTweaks = new List<BaseTweak>();
        var tweakCategoryList = new Dictionary<string, TweakCategoryContainer>();

        void ParseTweaks(IEnumerable<BaseTweak> tweaks) {
            foreach (var tweak in tweaks) {
                if (tweak is SubTweakManager stm) {
                    ParseTweaks(stm.GetTweakList());
                    continue;
                }
                
                if (!allTweaksList.Contains(tweak)) allTweaksList.Add(tweak);
                if (!uncategorizedTweaks.Contains(tweak)) uncategorizedTweaks.Add(tweak);
            }
        }
        ParseTweaks(plugin.Tweaks);
        _allTweaks = allTweaksList.OrderBy(t => t.LocalizedName).ToList();
        
        uncategorizedTweaks.RemoveAll(tweak => {
            var hasCategory = false;
            foreach (var category in tweak.Categories) {
                if (HiddenCategories.Contains(category)) continue;
                
                if (!tweakCategoryList.TryGetValue(category, out var categoryContainer)) {
                    categoryContainer = new TweakCategoryContainer(category);
                    tweakCategoryList.Add(category, categoryContainer);
                }

                if (!categoryContainer.Tweaks.Contains(tweak)) categoryContainer.Tweaks.Add(tweak);
                hasCategory = true;
            }
            
            if (!hasCategory && ShowOtherTweaksTab) {
                var other = $"{TweakCategory.Other}";
                if (!tweakCategoryList.TryGetValue(other, out var categoryContainer)) {
                    categoryContainer = new TweakCategoryContainer(other);
                    tweakCategoryList.Add(other, categoryContainer);
                }

                if (!categoryContainer.Tweaks.Contains(tweak)) categoryContainer.Tweaks.Add(tweak);
            }
            
            return hasCategory;
        });
        
        _tweakCategories = tweakCategoryList.Values.OrderBy(c => c.LocalizedName).ToList();
    }
    
    public void DrawConfigUI() {
        if (_allTweaks == null || _tweakCategories == null) { 
            BuildTweakList();
        }
        
        if (_allTweaks == null || _tweakCategories == null) { 
            ImGui.TextColored(ImGuiColors.DalamudRed, "The tweak list failed to load. Please report this.");
            return;
        }
        
        
        
        
        var allTweaks = _allTweaks;
        var tweakCategories = _tweakCategories;
        
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
            if (ImGui.BeginTabBar("tweakCategoryTabBar")) {

                if (ShowEnabledTweaksTab) {
                    if (_enabledTweaks == null || _enabledTweaks.Count == 0) _enabledTweaks = allTweaks.FindAll(t => t.Enabled);
                    var enabledTweaks = _enabledTweaks ?? new List<BaseTweak>();
                    MixColour(new Vector4(0.35f, 0.8f, 0.35f, -1), ImGuiCol.Tab, ImGuiCol.TabActive, ImGuiCol.TabHovered, ImGuiCol.TabUnfocused);

                    if (ImGui.BeginTabItem(Loc.Localize("Enabled Tweaks", "Enabled Tweaks", "Enabled Tweaks Tab Header") + "###enabledTweaksTab")) {
                        if (ImGui.BeginChild("enabledTweaks", new Vector2(-1, -1), false)) {
                            foreach (var tweak in enabledTweaks) {
                                if (!IsTweakVisible(tweak)) continue;
                                var enabled = tweak.Enabled;
                                if (!enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Text)) * new Vector4(1, 1, 1, 0.5f));
                                DrawTweakConfig(tweak, ref changed);
                                if (!enabled) ImGui.PopStyleColor();
                            }
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    } else {
                        _enabledTweaks = null;
                    }
                    ImGui.PopStyleColor(4);
                } else {
                    _enabledTweaks = null;
                }
                
                if (ShowAllTweaksTab) {
                    if (ImGui.BeginTabItem(Loc.Localize("All Tweaks", "All Tweaks", "All Tweaks Tab Header") + "###allTweaksTab")) {
                        if (ImGui.BeginChild("allTweaks", new Vector2(-1, -1), false)) {
                            foreach (var tweak in allTweaks) {
                                if (!IsTweakVisible(tweak)) continue;
                                DrawTweakConfig(tweak, ref changed);
                            }
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                foreach (var category in tweakCategories.OrderBy(t => t.CategoryName == $"{TweakCategory.Other}" ? 1 : 0).ThenBy(t => t.LocalizedName)) {
                    if (!category.Tweaks.Any(IsTweakVisible)) continue;
                    if (ImGui.BeginTabItem($"{category.LocalizedName}###tweakCategoryTab_{category}")) {
                        ImGui.BeginChild($"{category}-scroll", new Vector2(-1, -1));

                        if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue(category.CategoryName, out var description)) {
                            ImGui.TextDisabled($"{description}");
                            ImGui.Separator();
                        }
                        
                        foreach (var tweak in category.Tweaks) {
                            if (!IsTweakVisible(tweak)) continue;
                            DrawTweakConfig(tweak, ref changed);
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                if (ImGui.BeginTabItem(Loc.Localize("General Options / TabHeader", "General Options") + $"###generalOptionsTab")) {
                    ImGui.BeginChild($"generalOptions-scroll", new Vector2(-1, -1));

                    if (ImGui.Checkbox(Loc.Localize("General Options / Analytics Opt Out", "Opt out of metrics"), ref AnalyticsOptOut)) Save();
                    
                    #if DEBUG
                    ImGui.SameLine();
                    if (ImGui.Button("Report Metrics")) {
                        MetricsService.ReportMetrics();
                    }
                    #endif
                    
                    
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("Simple tweaks collects a list of enabled tweaks to give me an idea of which tweaks are being used. You can choose to opt out of this data collection and no information will be sent. No identifying information will be collected in any way.");

                    ImGui.Separator();

                    if (ImGui.CollapsingHeader(Loc.Localize("General Options / Visible Category Tabs", "Visible Category Tabs") + $" ({tweakCategories.Count + (ShowAllTweaksTab ? 1 : 0) + (ShowEnabledTweaksTab ? 1 : 0)})###visibleCategoryTabs") ) {
                        ImGui.Indent();

                        if (ImGui.Checkbox(LocalizedCategoryName("Enabled Tweaks"), ref ShowEnabledTweaksTab)) Save();
                        if (ImGui.Checkbox(LocalizedCategoryName("All Tweaks"), ref ShowAllTweaksTab)) Save();

                        string categoryDescription;
                        foreach (var c in HiddenCategories.Select(s => new TweakCategoryContainer(s)).Union(tweakCategories.Where(c => c.Tweaks.Any(IsTweakVisible))).OrderBy(c => c.LocalizedName)) {
                            if (c.CategoryName == $"{TweakCategory.Other}") continue;
                            if (c.CategoryName == $"{TweakCategory.Experimental}" && ShowExperimentalTweaks == false) continue;
                            
                            var isNotHidden = !HiddenCategories.Contains(c.CategoryName);
                            if (ImGui.Checkbox($"{c.LocalizedName}###tweakCategoryNotHidden_{c.CategoryName}", ref isNotHidden)) {
                                if (isNotHidden) {
                                    HiddenCategories.Remove(c.CategoryName);
                                } else {
                                    HiddenCategories.Add(c.CategoryName);
                                }
                                Save();
                                RebuildTweakList();
                            }

                            if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue(c.CategoryName, out categoryDescription)) {
                                ImGui.SameLine();
                                ImGuiComponents.HelpMarker(categoryDescription);
                            }
                            
                            
                        }

                        if (ImGui.Checkbox($"{LocalizedCategoryName(TweakCategory.Other)}###tweakCategoryNotHidden_{TweakCategory.Other}", ref ShowOtherTweaksTab)) {
                            Save();
                            RebuildTweakList();
                        }
                        
                        if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue($"{TweakCategory.Other}", out categoryDescription)) {
                            ImGui.SameLine();
                            ImGuiComponents.HelpMarker(categoryDescription);
                        }
                        
                        ImGui.Unindent();
                    }
                    ImGui.Separator();

                    if (ImGui.CollapsingHeader("Tweak List Display Options", ImGuiTreeNodeFlags.DefaultOpen)) {
                        ImGui.Indent();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Experimental Tweaks", "Show Experimental Tweaks."), ref ShowExperimentalTweaks)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak Descriptions","Show tweak descriptions."), ref ShowTweakDescriptions)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak IDs", "Show tweak IDs."), ref ShowTweakIDs)) Save();
                        ImGui.Separator();
                        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                        if (ImGui.BeginCombo(Loc.Localize("General Options / Festive Decorations", "Festive Decorations"), $"{FestiveDecorationType}")) {
                            foreach (var v in Enum.GetValues<DecorationType>().OrderBy(v => (int)v)) {
                                if (ImGui.Selectable($"{v}", FestiveDecorationType == v)) {
                                    FestiveDecorationType = v;
                                    Save();
                                }
                            }
                            
                            ImGui.EndCombo();
                        }
                        
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Hide KoFi", "Hide Ko-fi link."), ref HideKofi)) Save();
                        ImGui.Unindent();
                    }
                    ImGui.Separator();
#if DEBUG
                    if (ImGui.CollapsingHeader("Debug Options")) {
                        ImGui.Indent();
                        if (ImGui.Checkbox("Disable Auto Open", ref DisableAutoOpen)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox("Remove File Info From Logs", ref NoCallerInLog)) Save();
                        ImGui.Unindent();
                    }
                    ImGui.Separator();
#endif
                    if (ImGui.Button("Open Changelog")) {
                        plugin.ChangelogWindow.IsOpen = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.CollapsingHeader("Changelog Options", ImGuiTreeNodeFlags.DefaultOpen)) {
                        ImGui.Indent();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Auto Open Changelog", "Open New Changelogs Automatically"), ref AutoOpenChangelog)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Disable Changelog Notice", "Disable Changelog Notifications"), ref DisableChangelogNotification)) Save();
                        ImGui.Unindent();
                    } 
                    
                    ImGui.Separator();


                    if (ImGui.CollapsingHeader("Language & Localization", ImGuiTreeNodeFlags.DefaultOpen)) {
                        ImGui.Indent();
                        
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
                        ImGui.Unindent();
                    }
                    
                    
                    

                    ImGui.Separator();

                    var toggleableTweakManagers = plugin.Tweaks.Where(t => t is SubTweakManager { AlwaysEnabled: false }).Cast<SubTweakManager>().ToList();
                    if (toggleableTweakManagers.Count > 0) {
                        if (ImGui.CollapsingHeader($"Tweak Managers ({toggleableTweakManagers.Count(stm => stm.Enabled)}/{toggleableTweakManagers.Count} Enabled)###toggleableTweakManagers")) {
                            
                            ImGui.Indent();
                            ImGuiExt.TextWrappedDisabled("Tweak managers contain additional tweaks. If the manager is disabled all tweaks it contains will also be disabled until the manager is enabled again.");
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
                                ImGui.TreeNodeEx($"Enable Tweak Manager: {t.LocalizedName}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                                ImGui.Separator();
                            }
                            
                            ImGui.Unindent();
                        } else {
                            ImGui.Separator();
                        }
                    }
                    
                    

                    if (HiddenTweaks.Count > 0) {
                        if (ImGui.CollapsingHeader($"Hidden Tweaks ({HiddenTweaks.Count})###hiddenTweaks")) {
                            ImGui.Indent();
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
                            ImGui.Unindent();
                        }
                        ImGui.Separator();
                    }

                    if (CustomTweakProviders.Count > 0 || ShowExperimentalTweaks) {

                        if (ImGui.CollapsingHeader($"Tweak Providers ({CustomTweakProviders.Count(p => p.Enabled)}/{CustomTweakProviders.Count} Enabled)###tweakProviders")) {
                            ImGui.Indent();
                            ImGuiExt.TextWrappedDisabled("Tweak providers allow for loading tweaks from other sources. Only use providers created by someone you trust.");
                            CustomTweakProviderConfig? deleteCustomProvider = null;
                            for (var i = 0; i < CustomTweakProviders.Count; i++) {
                                if (ImGui.Button($"X##deleteCustomProvider_{i}")) {
                                    deleteCustomProvider = CustomTweakProviders[i];
                                }
                                ImGui.SameLine();
                                if (ImGui.Button($"R##reloadcustomProvider_{i}")) {
                                    foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                        if (tp.IsDisposed) continue;
                                        if (tp is not CustomTweakProvider ctp) continue;
                                        if (ctp.AssemblyPath == CustomTweakProviders[i].Assembly) {
                                            ctp.Dispose();
                                        }
                                    }
                                    plugin.LoadCustomProvider(CustomTweakProviders[i]);
                                    Loc.ClearCache();
                                }

                                ImGui.SameLine();
                                if (ImGui.Checkbox($"###customProvider_{i}", ref CustomTweakProviders[i].Enabled)) {

                                    foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                        if (tp.IsDisposed) continue;
                                        if (tp is not CustomTweakProvider ctp) continue;
                                        if (ctp.AssemblyPath == CustomTweakProviders[i].Assembly) {
                                            ctp.Dispose();
                                        }
                                        DebugManager.Reload();
                                    }
                                    
                                    if (CustomTweakProviders[i].Enabled) {
                                        plugin.LoadCustomProvider(CustomTweakProviders[i]);
                                    }
                                    
                                    Save();
                                }
                                ImGui.SameLine();
                                ImGui.Text(CustomTweakProviders[i].Assembly);
                            }

                            if (deleteCustomProvider != null) {
                                CustomTweakProviders.Remove(deleteCustomProvider);

                                foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                                    if (tp.IsDisposed) continue;
                                    if (tp is not CustomTweakProvider ctp) continue;
                                    if (ctp.AssemblyPath == deleteCustomProvider.Assembly) {
                                        ctp.Dispose();
                                    }
                                }
                                DebugManager.Reload();

                                Save();
                            }

                            if (ImGui.Button("+##addCustomProvider")) {
                                if (!string.IsNullOrWhiteSpace(addCustomProviderInput) && CustomTweakProviders.All(p => !p.Assembly.Equals(addCustomProviderInput, StringComparison.InvariantCultureIgnoreCase))) {
                                    var provider = new CustomTweakProviderConfig { Assembly = addCustomProviderInput, Enabled = true };
                                    CustomTweakProviders.Add(provider);
                                    SimpleTweaksPlugin.Plugin.LoadCustomProvider(provider);
                                    addCustomProviderInput = string.Empty;
                                    Save();
                                }
                            }

                            ImGui.SameLine();
                            ImGui.InputTextWithHint("##addCustomProviderInput", "File path to tweak provider DLL", ref addCustomProviderInput, 500);
                            ImGui.Unindent();
                        }
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
