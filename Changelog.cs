using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
        Add("1.8.3.0", "Added a changelog");
        Add("1.8.3.0", "Fixed graphical issue when resizing windows on clear blue theme.");
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

    private static bool ShouldShowTweak(BaseTweak tweak) {
        if (tweak == null) return false;
        var config = SimpleTweaksPlugin.Plugin.PluginConfig;

        // Don't show hidden tweaks
        if (config.HiddenTweaks.Contains(tweak.Key)) return false;
        
        // Don't show experimental tweaks, unless they're enabled or Experimental tweaks are enabled
        if (tweak.Experimental && !config.ShowExperimentalTweaks && !config.EnabledTweaks.Contains(tweak.Key)) return false;

        return true;
    }

    private static string GenerateChangelogMarkdown(Version changelogVersion = null, StringBuilder stringBuilder = null) {
        stringBuilder ??= new StringBuilder();

        if (changelogVersion == null) {
            stringBuilder.AppendLine("# Changelog");
            stringBuilder.AppendLine();
            foreach (var version in Entries.Keys.OrderByDescending(v => v)) {
                var versionLabel = version.Major == 99 ? "Unreleased" : $"{version}";

                stringBuilder.AppendLine($"## [{versionLabel}]");
                GenerateChangelogMarkdown(version, stringBuilder);
                stringBuilder.AppendLine();
            }

            
            return stringBuilder.ToString();
        }

        if (Entries.TryGetValue(changelogVersion, out var changelogs)) {
            
            var generalChanges = changelogs.Where(c => c.Tweak == null);
            var newTweaks = changelogs.Where(c => c.IsNewTweak && c.Tweak != null).OrderBy(c => c.Tweak.Name);
            var tweakChanges = changelogs.Where(c => c.Tweak != null && c.IsNewTweak == false).OrderBy(c => c.Tweak.Name);

            if (generalChanges.Any()) {

                if (newTweaks.Any() || tweakChanges.Any()) {
                    stringBuilder.AppendLine("***General Changes***");
                }

                foreach (var c in generalChanges) {
                    stringBuilder.Append($"> {c.Change}");
                    if (!string.IsNullOrEmpty(c.ChangeAuthor)) {
                        stringBuilder.Append($" *({c.ChangeAuthor})*");
                    }
                    stringBuilder.AppendLine();
                }
                
                if (newTweaks.Any() || tweakChanges.Any()) {
                    stringBuilder.AppendLine();
                }
            }

            if (newTweaks.Any()) {
                stringBuilder.AppendLine("***New Tweaks***");

                foreach (var c in newTweaks) {
                    stringBuilder.Append($"> **`{c.Tweak.Name}`** - {c.Tweak.Description.Split('\n')[0]}");
                    if (!string.IsNullOrEmpty(c.ChangeAuthor)) {
                        stringBuilder.Append($" *({c.ChangeAuthor})*");
                    }
                    stringBuilder.AppendLine("\n");
                }

                if (tweakChanges.Any()) {
                    stringBuilder.AppendLine();
                }
            }

            if (tweakChanges.Any()) {
                stringBuilder.AppendLine("***Tweak Changes***");

                var tweakChangeGroups = tweakChanges.GroupBy(c => c.Tweak);
                
                foreach (var g in tweakChangeGroups) {
                    if (!g.Any()) continue; // Who knows
                    if (g.Count() >= 2) {
                        
                        stringBuilder.AppendLine($"> **`{g.Key.Name}`**");

                        foreach (var c in g) {
                            if (c.Tweak != g.Key) continue;
                            stringBuilder.Append($"> - {c.Change}");
                            if (!string.IsNullOrEmpty(c.ChangeAuthor)) {
                                stringBuilder.Append($" *({c.ChangeAuthor})*");
                            }
                            stringBuilder.AppendLine();
                        }

                        stringBuilder.AppendLine();

                    } else {
                        var c = g.First();
                        if (c.Tweak == null) continue;
                        stringBuilder.Append($"> **`{c.Tweak.Name}`** - {c.Change}");
                        if (!string.IsNullOrEmpty(c.ChangeAuthor)) {
                            stringBuilder.Append($" *({c.ChangeAuthor})*");
                        }
                        stringBuilder.AppendLine("\n");
                    }
                    
                   
                }
            }
            
            
            
        }
        
        return stringBuilder.ToString();
    }
    
    public override void Draw() {
#if DEBUG
        if (ImGui.Button("Copy Full Changelog")) {
            ImGui.SetClipboardText(GenerateChangelogMarkdown());
        }
#endif       
        foreach (var (version, changelogs) in Entries.OrderByDescending(v => v.Key)) {

#if DEBUG
            if (ImGui.Button($"C##{version}")) {
                ImGui.SetClipboardText(GenerateChangelogMarkdown(version));
            }
            ImGui.SameLine();
#endif
            
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

                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip($"Click to find '{c.Tweak.Name}' in Simple Tweaks config window");
                        }
                        
                        if (c.ChangeAuthor != null) {
                            ImGui.SameLine();
                            ImGui.TextDisabled($"({c.ChangeAuthor})");
                        }

                        if (tweakTreeOpen) {
                            ImGui.Indent();
                            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                            ImGui.TextWrapped(c.Tweak.Description);
                            ImGui.PopStyleColor();
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
