using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System.Collections.Generic;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Try On Correct Item")]
[TweakDescription("Show the correct item when trying on a glamoured item.")]
[TweakAutoConfig]
public class TryOnCorrectItem : Tweak {
    [TweakHook(typeof(AgentTryon), nameof(AgentTryon.TryOn), nameof(TryOnDetour))]
    private HookWrapper<AgentTryon.Delegates.TryOn> tryOnHook;

    private List<string> newWindows = [];

    public enum TryOnItem {
        Original,
        Glamoured,
    }

    public class TryOnWindowSettings {
        public TryOnItem NoModifier = TryOnItem.Glamoured;
        public TryOnItem HoldingShift = TryOnItem.Original;
    }

    public class Configs : TweakConfig {
        public TryOnWindowSettings Default = new();
        public Dictionary<string, TryOnWindowSettings> WindowSettings = new();
    }

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
        using var table = ImRaii.Table("tryOnCorrectItemSettings", 4,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoBordersInBody);
        if (!table) return;

        List<string> removalQueue = [];

        ImGui.TableSetupColumn("Window", ImGuiTableColumnFlags.WidthFixed, 180 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("No Modifier", ImGuiTableColumnFlags.WidthFixed, 130 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("Holding Shift", ImGuiTableColumnFlags.WidthFixed, 130 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("Manage", ImGuiTableColumnFlags.WidthFixed, 80 * ImGui.GetIO().FontGlobalScale);

        ImGui.TableHeadersRow();

        bool Editor(string name, TryOnWindowSettings settings)
        {
            var editorChanged = true;

            ImGui.PushID($"tryOnCorrectWindowSettings_{name}");
            ImGui.TableNextColumn();
            ImGui.Text(name);
            ImGui.TableNextColumn();

            void ValueEditor(string key, ref TryOnItem value)
            {
                ImGui.SetNextItemWidth(-1);
                using var combo = ImRaii.Combo($"##{key}", $"{value}", ImGuiComboFlags.None);
                if (!combo) return;
                if (ImGui.Selectable($"{TryOnItem.Original}", value == TryOnItem.Original)) {
                    value = TryOnItem.Original;
                    editorChanged = true;
                }
                if (ImGui.Selectable($"{TryOnItem.Glamoured}", value == TryOnItem.Glamoured)) {
                    value = TryOnItem.Glamoured;
                    editorChanged = true;
                }
            }

            ValueEditor("No Modifier", ref settings.NoModifier);
            ImGui.TableNextColumn();
            ValueEditor("Holding Shift", ref settings.HoldingShift);

            ImGui.TableNextColumn();
            if (name != "Default") {
                if (ImGui.Button("Remove")) {
                    removalQueue.Add(name);
                }
            }

            ImGui.PopID();

            return editorChanged;
        }

        hasChanged |= Editor("Default", Config.Default);
        foreach (var (name, settings) in Config.WindowSettings) {
            hasChanged |= Editor(name, settings);
        }

        if (newWindows.Count > 0) {
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            using var combo = ImRaii.Combo("##tryOnCorrectItemSettings_newWindow", "Select Window to Add...",
                ImGuiComboFlags.None);
            if (combo) {
                foreach (var window in newWindows) {
                    if (ImGui.Selectable(window, false)) {
                        if (!Config.WindowSettings.ContainsKey(window)) {
                            Config.WindowSettings.Add(window, new TryOnWindowSettings
                            {
                                HoldingShift = Config.Default.HoldingShift,
                                NoModifier = Config.Default.NoModifier,
                            });
                        }
                        newWindows.Remove(window);
                        break;
                    }
                }
            }
        }

        foreach (var name in removalQueue) {
            Config.WindowSettings.Remove(name);
        }
    }

    private unsafe TryOnWindowSettings GetActiveWindowSettings() {
        var addonId = 0U;
        var agentContext = AgentContext.Instance();
        if (agentContext != null && agentContext->AgentInterface.IsAgentActive()) {
            addonId = agentContext->OwnerAddon;
        } else {
            var agentInventoryContext = AgentInventoryContext.Instance();
            if (agentInventoryContext != null && agentInventoryContext->AgentInterface.IsAgentActive()) {
                addonId = agentInventoryContext->OwnerAddonId;
            }
        }

        if (addonId is 0 or > ushort.MaxValue) return Config.Default;
        var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonById((ushort)addonId);
        if (addon == null) return Config.Default;
        var addonName = addon->NameString;
        if (string.IsNullOrEmpty(addonName)) return Config.Default;
        SimpleLog.Log($"Try on from : {addonName}");
        if (Config.WindowSettings.TryGetValue(addonName, out var settings)) return settings;
        if (!newWindows.Contains(addonName)) {
            newWindows.Add(addonName);
        }

        return Config.Default;
    }

    private bool TryOnDetour(uint openerAddonId, uint itemId, byte stain0Id, byte stain1Id, uint glamourItemId, bool applyCompanyCrest)
    {
        var c = GetActiveWindowSettings();
        var i = c.NoModifier;
        if (Service.KeyState[VirtualKey.SHIFT]) i = c.HoldingShift;
        return tryOnHook.Original(openerAddonId, itemId, stain0Id, stain1Id, i == TryOnItem.Original ? itemId : glamourItemId, applyCompanyCrest);
    }
}