using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin;

public class ChangelogEntry {
    public BaseTweak? Tweak { get; }
    public Version Version { get; }
    public string Change { get; } = string.Empty;
    public bool IsNewTweak { get; }
    public string? ChangeAuthor { get; private set; }

    public ChangelogEntry(BaseTweak tweak, string version, string log, bool isNewTweak = false) {
        Tweak = tweak;
        Version = Version.Parse(version);
        Change = log;
        IsNewTweak = isNewTweak;
    }

    public ChangelogEntry(string version, string log) {
        Version = Version.Parse(version);
        Change = log;
    }

    public ChangelogEntry Author(string? author) {
        ChangeAuthor = author;
        return this;
    }
}

public class Changelog : Window {
    internal static void AddGeneralChangelogs() {
        Add(Changelog.UnreleasedVersion, "Added a changelog");
        Add("1.8.2.0", "Now using the Dalamud Window system.\nESC will now close Simple Tweaks windows.");
    }

#if DEBUG
    public const string UnreleasedVersion = "99.99.99.99";
#endif 

    private static Dictionary<Version, List<ChangelogEntry>> Entries = new();

    public static ChangelogEntry Add(BaseTweak tweak, string version, string log) {
        var changelog = new ChangelogEntry(tweak, version, log);
        Add(changelog);
        return changelog;
    }

    public static ChangelogEntry AddNewTweak(BaseTweak tweak, string version) {
        var changelog = new ChangelogEntry(tweak, version, string.Empty, isNewTweak: true);
        Add(changelog);
        return changelog;
    }

    public static ChangelogEntry Add(string version, string log) {
        var changelog = new ChangelogEntry(version, log);
        Add(changelog);
        return changelog;
    }

    private static void Add(ChangelogEntry changelog) {
        if (!Entries.ContainsKey(changelog.Version)) Entries.Add(changelog.Version, new List<ChangelogEntry>());
        Entries[changelog.Version].Add(changelog);
        if (HasNewChangelog || SimpleTweaksPlugin.Plugin.ChangelogWindow.IsOpen) return;
        
        if (Version.TryParse(SimpleTweaksPlugin.Plugin.PluginConfig.LastSeenChangelog, out var lastVersion)) {
            #if DEBUG
                HasNewChangelog = !SimpleTweaksPlugin.Plugin.PluginConfig.DisableChangelogNotification && lastVersion < changelog.Version && changelog.Version.Major != 99;
            #else
                HasNewChangelog = !SimpleTweaksPlugin.Plugin.PluginConfig.DisableChangelogNotification && lastVersion < changelog.Version;    
            #endif

        } else {
            HasNewChangelog = !SimpleTweaksPlugin.Plugin.PluginConfig.DisableChangelogNotification;
        }
        
        if (HasNewChangelog && SimpleTweaksPlugin.Plugin.PluginConfig.AutoOpenChangelog) {
            SimpleTweaksPlugin.Plugin.ChangelogWindow.IsOpen = true;
        }
    }
    
    public Version CurrentVersion { get; }

    public static bool HasNewChangelog { get; private set; } = false;

    public Changelog() : base("###simpleTweaksChangelog") {
        CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        WindowName = $"Simple Tweaks Changelog ({CurrentVersion})###simpleTweaksChangelog";
        Size = ImGuiHelpers.ScaledVector2(600, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen() {
        HasNewChangelog = false;
        SimpleTweaksPlugin.Plugin.PluginConfig.LastSeenChangelog = $"{CurrentVersion}";
        SimpleTweaksPlugin.Plugin.PluginConfig.Save();
        base.OnOpen();
    }

    private bool ShouldShowTweak(BaseTweak tweak) {
        if (tweak == null) return false;
        var config = SimpleTweaksPlugin.Plugin.PluginConfig;

        // Don't show hidden tweaks
        if (config.HiddenTweaks.Contains(tweak.Key)) return false;
        
        // Don't show experimental tweaks, unless they're enabled or Experimental tweaks are enabled
        if (tweak.Experimental && !config.ShowExperimentalTweaks && !config.EnabledTweaks.Contains(tweak.Key)) return false;

        return true;
    }

    public override void Draw() {

        foreach (var (version, changelogs) in Entries.OrderByDescending(v => v.Key)) {

            var flags = CurrentVersion == version ? ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.CollapsingHeader : ImGuiTreeNodeFlags.CollapsingHeader;
#if DEBUG
            if (version.Major == 99) flags |= ImGuiTreeNodeFlags.DefaultOpen;
            var versionHeaderText = version.Major == 99 ? "Unreleased" : $"Version {version}";
#else
            var versionHeaderText = $"Version {version}";
#endif

            if (flags.HasFlag(ImGuiTreeNodeFlags.DefaultOpen)) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);

            var versionHeaderOpen = ImGui.CollapsingHeader($"{versionHeaderText}##ChangeLogHeader", flags);
            
            if (flags.HasFlag(ImGuiTreeNodeFlags.DefaultOpen)) ImGui.PopStyleColor();
            
            if (!versionHeaderOpen) continue;

            var generalChanges = changelogs.Where(c => c.Tweak == null);
            
            if (generalChanges.Any()) {
                if (ImGui.TreeNodeEx($"General Changes##{version}", ImGuiTreeNodeFlags.DefaultOpen)) {
                    foreach (var c in generalChanges) {
                        ImGui.TreeNodeEx(c.Change, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet);
                        if (c.ChangeAuthor != null) {
                            ImGui.SameLine();
                            ImGui.TextDisabled($"({c.ChangeAuthor})");
                        }
                    }
                    ImGui.TreePop();
                }
            }

            var newTweaks = changelogs.Where(c => c.IsNewTweak && c.Tweak != null && ShouldShowTweak(c.Tweak)).OrderBy(c => c.Tweak.Name);
            if (newTweaks.Any()) {
                if (ImGui.TreeNodeEx($"New Tweaks##{version}", ImGuiTreeNodeFlags.DefaultOpen)) {
                    foreach (var c in newTweaks) {
                        if (c.Tweak == null) continue;

                        ImGui.BeginGroup();

                        var tweakTreeOpen = ImGui.TreeNodeEx(c.Tweak.Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
                        
                        if (ImGui.IsItemClicked()) {
                            SimpleTweaksPlugin.Plugin.ConfigWindow.IsOpen = true;
                            SimpleTweaksPlugin.Plugin.PluginConfig.FocusTweak(c.Tweak);
                        }
                        
                        if (c.ChangeAuthor != null) {
                            ImGui.SameLine();
                            ImGui.TextDisabled($"({c.ChangeAuthor})");
                        }

                        if (tweakTreeOpen) {
                            ImGui.Indent();
                            ImGui.TextDisabled(c.Tweak.Description);
                            ImGui.Unindent();
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }
            }

            var tweakChanges = changelogs.Where(c => c.Tweak != null && c.IsNewTweak == false && ShouldShowTweak(c.Tweak)).GroupBy(c => c.Tweak);
            if (tweakChanges.Any()) {
                if (ImGui.TreeNodeEx($"Tweak Changes##{version}", ImGuiTreeNodeFlags.DefaultOpen)) {
                    foreach (var group in tweakChanges.OrderBy(g => g.Key.Name)) {
                        var tweak = group.Key;

                        var xBefore = ImGui.GetCursorPosX();
                        
                        var open = ImGui.TreeNodeEx($"{tweak.Name}##{version}_{tweak.Key}", ImGuiTreeNodeFlags.DefaultOpen);
                        var xAfter = ImGui.GetCursorPosX();
                        ImGui.SameLine();
                        
                        ImGui.SetCursorPosX(xBefore - ImGui.GetIO().FontGlobalScale * 16);
                        if (ImGui.SmallButton($">###{version}_{tweak.Key}_openSettings")) {
                            SimpleTweaksPlugin.Plugin.ConfigWindow.IsOpen = true;
                            SimpleTweaksPlugin.Plugin.PluginConfig.FocusTweak(tweak);
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Find '{tweak.Name}' in Simple Tweaks config");
                        ImGui.SetCursorPosX(xAfter);

                        if (open) {
                            foreach (var c in group) {
                                ImGui.TreeNodeEx(c.Change, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet);
                                if (c.ChangeAuthor != null) {
                                    ImGui.SameLine();
                                    ImGui.TextDisabled($"({c.ChangeAuthor})");
                                }
                            }
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}
