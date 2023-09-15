#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using SignatureHelper = Dalamud.Utility.Signatures.SignatureHelper;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("MSQ Progress Display")]
[TweakDescription("Displays percentage of progress through Main Scenario Quests in the Scenario Guide.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class MainStoryQuestProgressDisplay : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public bool ShowPerExpansion;
    }

    public Configs Config { get; private set; } = null!;

    private delegate void ScenarioTreeUpdateDelegate(AtkUnitBase* addon);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 49 8B F8 8B F2 48 8B D9 85 D2 0F 84 ?? ?? ?? ?? 4D 85 C0 0F 84 ?? ?? ?? ?? 49 8B C8 E8 ?? ?? ?? ?? 85 C0 0F 88 ?? ?? ?? ??", DetourName = nameof(OnScenarioTreeRefresh))]
    private readonly Hook<ScenarioTreeUpdateDelegate>? onScenarioTreeUpdateHook = null;

    // Dictionary<ExpansionIndex, ScenarioTreeStep>
    private Dictionary<uint, ushort> expansionStops = new();

    protected override void ConfigChanged() {
        SaveConfig(Config);
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool changed) => {
        if (ImGui.RadioButton("Total MSQ Progression", !Config.ShowPerExpansion)) {
            Config.ShowPerExpansion = false;
            changed = true;
        }
        if (ImGui.RadioButton("Current Expansion Progression", Config.ShowPerExpansion)) {
            Config.ShowPerExpansion = true;
            changed = true;
        }
    };

    public override void Setup() {
        SignatureHelper.Initialise(this);
        expansionStops = CalculateExpansionStops();
        base.Setup();
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        onScenarioTreeUpdateHook?.Enable();
        UpdateScenarioTreeText(Common.GetUnitBase<AtkUnitBase>("ScenarioTree"));
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        onScenarioTreeUpdateHook?.Disable();
        ResetScenarioTreeText(Common.GetUnitBase<AtkUnitBase>("ScenarioTree"));
        base.Disable();
    }

    public override void Dispose() {
        onScenarioTreeUpdateHook?.Dispose();
        base.Dispose();
    }

    private void OnScenarioTreeRefresh(AtkUnitBase* addon) {
        onScenarioTreeUpdateHook!.Original(addon);

        try {
            UpdateScenarioTreeText(addon);
        }
        catch (Exception e) {
            SimpleLog.Error(e);
        }
    }

    private float CalculateProgress(ushort step) {
        if (Service.Data.GetExcelSheet<ScenarioTree>()!.FirstOrDefault(entry => entry.Unknown1 == step) is { RowId: var currentQuest }) {
            if (Config.ShowPerExpansion) {
                var currentExpansion = Service.Data.GetExcelSheet<Quest>()!.GetRow(currentQuest)!.Expansion.Row;

                var minIndex = currentExpansion is 0 ? 0 : expansionStops[currentExpansion - 1];
                var maxIndex = expansionStops[currentExpansion];
                var range = maxIndex - minIndex;
                var relativePosition = step - minIndex;

                return (float) relativePosition / range * 100.0f;
            }
            else {
                var maxIndex = expansionStops.Values.Max();

                return (float) step / maxIndex * 100.0f;
            }
        }

        return float.MinValue;
    }

    private void UpdateScenarioTreeText(AtkUnitBase* addon) {

        if (addon is null) return;
        
        if (addon->AtkValues[6] is not { Type: ValueType.String, String: var stringValue }) return;
        if (addon->AtkValues[7] is not { Type: ValueType.String, String: var originalLabel }) return;

        var indexString = MemoryHelper.ReadStringNullTerminated((nint) stringValue);
        if (indexString.IsNullOrEmpty()) return;

        var questLabel = MemoryHelper.ReadStringNullTerminated((nint) originalLabel);
        if (questLabel.IsNullOrEmpty()) return;

        if (!ushort.TryParse(indexString, out var indexValue)) return;

        var buttonNode = addon->GetButtonNodeById(13);
        if (buttonNode is null) return;

        // If the text is modified, reset it
        if (buttonNode->ButtonTextNode->NodeText.ToString() != questLabel) {
            buttonNode->ButtonTextNode->NodeText.SetString(questLabel);
        }

        var progress = CalculateProgress(indexValue);
        if (progress is float.MinValue) return;

        buttonNode->ButtonTextNode->SetText(buttonNode->ButtonTextNode->NodeText + $" ({progress:0#.#}%)");
    }

    private void ResetScenarioTreeText(AtkUnitBase* addon) {
        
        if (addon is null) return;
        
        if (addon->AtkValues[7] is not { Type: ValueType.String, String: var originalLabel }) return;
        
        var questLabel = MemoryHelper.ReadStringNullTerminated((nint) originalLabel);
        if (questLabel.IsNullOrEmpty()) return;
        
        var buttonNode = addon->GetButtonNodeById(13);
        if (buttonNode is null) return;
        
        if (buttonNode->ButtonTextNode->NodeText.ToString() != questLabel) {
            buttonNode->ButtonTextNode->NodeText.SetString(questLabel);
        }
    }

    private static Dictionary<uint, ushort> CalculateExpansionStops() =>
        Service.Data.GetExcelSheet<ScenarioTree>()!
            .GroupBy(treeEntry => Service.Data.GetExcelSheet<Quest>()!.GetRow(treeEntry.RowId)!.Expansion.Row)
            .ToDictionary(group => group.Key, group => group.Max(entry => entry.Unknown1));
}