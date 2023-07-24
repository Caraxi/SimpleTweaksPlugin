using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Dalamud.Game.Config;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using ImGuiNET;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class ConfigDebug : DebugHelper {
    public override string Name => "Config View";

    private string nameSearchString = string.Empty;

    private void DrawConfigBase(ConfigBase* configBase) {
        ImGui.Text($"Config Count: {configBase->ConfigCount}");
        ImGui.Text($"{configBase->UnkString.ToString()}");

        DebugManager.PrintAddress(configBase);
        ImGui.SameLine();
        DebugManager.PrintOutObject(configBase);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"###configNameSearch", ref nameSearchString, 50);

        if (ImGui.BeginTable($"configTable", 8, ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable)) {
            void SetupTableColumns() {
                ImGui.TableSetupColumn("  #", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().KeyShift ? 100 : 45);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 250);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Default", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Min", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Max", ImGuiTableColumnFlags.WidthFixed, 80);
            }

            SetupTableColumns();
            ImGui.TableHeadersRow();

            var insideHeader = false;
            var currentHeaderOpen = false;

            var configEntry = configBase->ConfigEntry;
            for (var i = 0; i < configBase->ConfigCount; i++) {
                if (configEntry->Type == 0) goto Continue;
                var name = $"#{i};";
                if (configEntry->Name != null)
                    name = MemoryHelper.ReadStringNullTerminated(new IntPtr(configEntry->Name));

                if (!string.IsNullOrWhiteSpace(nameSearchString)) {
                    if (!name.Contains(nameSearchString, StringComparison.InvariantCultureIgnoreCase)) goto Continue;
                }

                if (configEntry->Type == 1 && string.IsNullOrWhiteSpace(nameSearchString)) {
                    ImGui.EndTable();

                    if (ImGui.GetIO().KeyShift) {
                        DebugManager.ClickToCopy(configEntry);
                    } else {
                        DebugManager.ClickToCopyText($"{i}".PadLeft($"{configBase->ConfigCount}".Length, '0'), $"{(ulong)configEntry:X}");
                    }

                    ImGui.SameLine();
                    currentHeaderOpen = ImGui.TreeNodeEx($"{name}###configHeader_{i}", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    insideHeader = true;

                    ImGui.BeginTable("configTable", 8, ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Reorderable);
                    SetupTableColumns();

                    goto Continue;
                } else if (insideHeader && !currentHeaderOpen) goto Continue;

                ImGui.TableNextColumn();
                if (ImGui.GetIO().KeyShift) {
                    DebugManager.ClickToCopy(configEntry);
                } else {
                    DebugManager.ClickToCopyText($"{i}".PadLeft($"{configBase->ConfigCount}".Length, '0'), $"{(ulong)configEntry:X}");
                }

                ImGui.TableNextColumn();
                ImGui.Text($"{(ConfigType)configEntry->Type}");

                ImGui.TableNextColumn();
                ImGui.Text($"{name}");

                var valueString = configEntry->Type switch {
                    0 => $"{configEntry->Value.UInt}",
                    1 => $"{configEntry->Value.Float}",
                    2 => $"{configEntry->Value.UInt}",
                    3 => $"{configEntry->Value.Float}",
                    4 => $"{configEntry->Value.String->ToString()}",
                    _ => $"[TYPE{configEntry->Type}]{(ulong)configEntry->Value.String:X}"
                };

                ImGui.TableNextColumn();
                ImGui.Text($"{valueString}");
                ImGui.TableNextColumn();

                switch (configEntry->Type) {
                    case 2:
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.UInt.DefaultValue}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.UInt.MinValue}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.UInt.MaxValue}");
                        break;
                    case 3:
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.Float.DefaultValue}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.Float.MinValue}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.Float.MaxValue}");
                        break;
                    case 4:
                        ImGui.TableNextColumn();
                        ImGui.Text($"{configEntry->Properties.String.DefaultValue->ToString()}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        break;
                }

                // ImGui.Text($"[{i}] {name} => {valueString}        [{configEntry->Type}]");

                Continue:
                configEntry++;
            }

            ImGui.EndTable();
        }
    }

    private string testSetterString = string.Empty;
    private int testSetterInt = 0;
    private float testSetterFloat = 0;

    public override void Draw() {
        if (ImGui.BeginTabBar("ConfigTabs")) {
            if (ImGui.BeginTabItem("System")) {
                DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.ConfigBase);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("UiConfig")) {
                DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiConfig);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("UiControl")) {
                if (ImGui.BeginTabBar("uiControlTabs")) {
                    if (ImGui.BeginTabItem("Current Setting")) {
                        var padMode = Service.GameConfig.UiConfig.GetUInt("PadMode");
                        if (padMode == 0) {
                            ImGui.Text("Keyboard & Mouse");
                            DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlConfig);
                        } else {
                            ImGui.Text("Controller");
                            DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlGamepadConfig);
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Mouse & Keyboard")) {
                        DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlConfig);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Controller")) {
                        DrawConfigBase(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlGamepadConfig);
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dev")) {
                DrawConfigBase(&Framework.Instance()->DevConfig.CommonDevConfig.ConfigBase);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Changes")) {
                DrawConfigChanges();
                ImGui.EndTabItem();
            }
            

            if (ImGui.BeginTabItem("Generate Enums")) {
                if (ImGui.BeginTabBar("generateDalamudTabs")) {
                    var sb = new StringBuilder();

                    void Generate(ConfigBase* configBase, Type e) {
                        var counts = new Dictionary<string, int>();
                        var lastName = string.Empty;
                        if (configBase != null) {
                            var configEntry = configBase->ConfigEntry;

                            var a = new Dictionary<int, string>();
                            var newEntries = new Dictionary<string, string>();

                            for (var i = 0; i < configBase->ConfigCount; i++) {
                                if (configEntry->Name == null && (string.IsNullOrEmpty(lastName) || configEntry->Type == 0)) {
                                    goto Continue;
                                }
                                var name = configEntry->Name == null ? lastName : MemoryHelper.ReadStringNullTerminated(new IntPtr(configEntry->Name));
                                lastName = name;
                                if (!counts.ContainsKey(name)) {
                                    counts.Add(name, 0);
                                }

                                counts[name]++;

                                var entry = new StringBuilder();

                                var type = (ConfigType)configEntry->Type;
                                if (!name.StartsWith("<")) {
                                    entry.AppendLine($"    /// <summary>");
                                    entry.AppendLine($"    /// System option with the internal name {name}.");
                                    entry.AppendLine($"    /// This option is a {type}.");

                                    entry.AppendLine($"    /// </summary>");
                                    if (counts[name] > 1) {
                                        entry.AppendLine($"    [GameConfigOption(\"{name}\", ConfigType.{type}, EntryCount = {counts[name]})]");
                                    } else {
                                        entry.AppendLine($"    [GameConfigOption(\"{name}\", ConfigType.{type})]");
                                    }

                                    entry.Append($"    {name}");

                                    if (Enum.TryParse(e, name, out var idxObj)) {
                                        a.TryAdd((int)idxObj, entry.ToString());
                                        a[(int)idxObj] = entry.ToString();
                                    } else {
                                        newEntries.TryAdd(name, entry.ToString());
                                        newEntries[name] = entry.ToString();
                                    }
                                }

                                Continue:
                                configEntry++;
                            }

                            var last = -1;

                            foreach (var (idx, entry) in a.OrderBy(k => k.Key)) {
                                var entryText = entry;
                                if (idx != last + 1) {
                                    entryText = $"{entry} = {idx}";
                                }

                                if (last != -1) {
                                    sb.AppendLine();
                                }

                                last = idx;

                                entryText = $"{entryText},";

                                sb.AppendLine(entryText);
                            }

                            foreach (var entry in newEntries) {
                                sb.AppendLine();
                                sb.AppendLine($"{entry.Value},");
                            }

                            sb.AppendLine("}");

                            if (ImGui.Button("Copy to Clipboard")) {
                                ImGui.SetClipboardText($"{sb}");
                            }

                            if (ImGui.BeginChild("generateScroll", ImGui.GetContentRegionAvail(), true)) {
                                foreach (var s in sb.ToString().Split('\n')) {
                                    ImGui.Text($"{s.TrimEnd()}");
                                }
                            }

                            ImGui.EndChild();
                        }
                    }

                    if (ImGui.BeginTabItem("System##enum")) {
                        sb.AppendLine(@"using FFXIVClientStructs.FFXIV.Common.Configuration;

namespace Dalamud.Game.Config;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

/// <summary>
/// Config options in the System section.
/// </summary>
public enum SystemConfigOption
{");

                        Generate(&Framework.Instance()->SystemConfig.CommonSystemConfig.ConfigBase, typeof(SystemConfigOption));

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("UiConfig##enum")) {
                        sb.AppendLine(@"using FFXIVClientStructs.FFXIV.Common.Configuration;

namespace Dalamud.Game.Config;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

/// <summary>
/// Config options in the UiConfig section.
/// </summary>
public enum UiConfigOption
{");
                        Generate(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiConfig, typeof(UiConfigOption));
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("UiControl##enum")) {
                        sb.AppendLine(@"using FFXIVClientStructs.FFXIV.Common.Configuration;

namespace Dalamud.Game.Config;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

/// <summary>
/// Config options in the UiControl section.
/// </summary>
public enum UiControlOption
{");
                        Generate(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlConfig, typeof(UiControlOption));
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    
    
    
    
    private void DrawConfigChanges() {
        Service.GameConfig.Changed -= OnConfigChange;
        Service.GameConfig.Changed += OnConfigChange;

        if (ImGui.Button("Clear")) changes.Clear();
        
        if (ImGui.BeginChild("changes", ImGui.GetContentRegionAvail(), true)) {
            foreach (var s in changes) {
                ImGui.Text($"{s}");
            }
        }
        ImGui.EndChild();
        
        
    }


    private List<string> changes = new();

    private PropertyInfo propertyInfo;
    private Dictionary<Enum, ConfigType?> typeCache = new();
    private (ConfigType?, string? name) GetConfigDetail(Enum e) {
        // TODO: Make this sane again when dalamud uses its own config type enum.
        var attr = e.GetAttribute<GameConfigOptionAttribute>();
        // return (attr?.Type, attr?.Name);
        
        if (typeCache.TryGetValue(e, out var v)) return (v, attr?.Name);
        
        if (attr != null) {
            propertyInfo ??= attr.GetType().GetProperty("Type", BindingFlags.Instance | BindingFlags.Public);
            if (propertyInfo != null) {
                var typeObj = propertyInfo!.GetValue(attr);
                if (typeObj != null) {
                    v = (ConfigType) typeObj;
                }
            }
        }

        typeCache.TryAdd(e, v);
        return (v, attr?.Name);
    }
    
    private void OnConfigChange(object sender, ConfigChangeEvent e) {
        
        var section = e.Option switch {
            SystemConfigOption => Service.GameConfig.System,
            UiConfigOption => Service.GameConfig.UiConfig,
            UiControlOption => Service.GameConfig.UiControl,
            _ => null
        };
        
        if (section == null) return;

        try {
            var (type, name) = GetConfigDetail(e.Option);
            if (name == null) return;
            switch (type) {
                case ConfigType.UInt: {
                    if (section.TryGet(name, out uint value)) {
                        changes.Insert(0, $"{section.SectionName}.{name} = {value}");
                    }

                    break;
                }
                case ConfigType.Float: {
                    if (section.TryGet(name, out float value)) {
                        changes.Insert(0, $"{section.SectionName}.{name} = {value}");
                    }
                    break;
                }
                   
                case ConfigType.String: {
                    if (section.TryGet(name, out string value)) {
                        changes.Insert(0, $"{section.SectionName}.{name} = {value}");
                    }
                    break;
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        
        if (changes.Count > 1000) changes.RemoveRange(1000, 1);


    }

    public override void Dispose() {
        Service.GameConfig.Changed -= OnConfigChange;
        base.Dispose();
    }
}
