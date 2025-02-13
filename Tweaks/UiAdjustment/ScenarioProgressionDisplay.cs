using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Scenario Progression Display")]
[TweakDescription("Shows the percentage of completion of the main scenario.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.0.0")]
[Changelog("1.10.8.0", "Once again fixed logic.")]
public unsafe class ScenarioProgressionDisplay : UiAdjustments.SubTweak {

    private ScenarioTree? finalScenario;
    private readonly Dictionary<uint, ScenarioTree> expansionBegins = new();
    private readonly Dictionary<uint, ScenarioTree> expansionEnds = new();

    public class Config : TweakConfig {
        [TweakConfigOption("Show for current expansion", 1)]
        public bool UseCurrentExpansion;

        [TweakConfigOption("Show percentage before quest", 2)]
        public bool ShowBeforeQuest;

        [TweakConfigOption("Percentage Accuracy", 2, IntMin = 0, IntMax = 3, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EnforcedLimit = true, EditorSize = 100)]
        public int Accuracy = 1;
    }

    [TweakConfig] public Config TweakConfig { get; private set; }

    protected override void Enable() => UpdateAddon(Common.GetUnitBase("ScenarioTree"));
    protected override void Disable() => UpdateAddon(Common.GetUnitBase("ScenarioTree"));
    protected override void ConfigChanged() => UpdateAddon(Common.GetUnitBase("ScenarioTree"));

    private float GetScenarioCompletionForCurrentExpansion() {
        var current = GetCurrentScenarioTreeEntry();
        if (current == null) return 0;
        if (!Service.Data.GetExcelSheet<Quest>().TryGetRow(current.Value.RowId, out var quest)) return 0;
        return GetScenarioCompletion(quest.Expansion.Value);
    }

    private float GetScenarioCompletion(ExVersion? expansion = null) {
        var current = GetCurrentScenarioTreeEntry();
        if (current == null) return 0;
        
        if (finalScenario == null) {
            foreach (var st in Service.Data.GetExcelSheet<ScenarioTree>()!) {
                if (!Service.Data.GetExcelSheet<Quest>().TryGetRow(st.RowId, out var quest)) continue;
                if (!quest.Expansion.IsValid) continue;

                if (finalScenario == null || st.Unknown2 > finalScenario.Value.Unknown2) finalScenario = st;
                expansionEnds.TryAdd(quest.Expansion.Value.RowId, st);
                expansionBegins.TryAdd(quest.Expansion.Value.RowId, st);

                if (st.Unknown2 > expansionEnds[quest.Expansion.Value.RowId].Unknown2) {
                    expansionEnds[quest.Expansion.Value.RowId] = st;
                }

                if (st.Unknown2 < expansionBegins[quest.Expansion.Value.RowId].Unknown2) {
                    expansionBegins[quest.Expansion.Value.RowId] = st;
                }
            }
        }

        if (finalScenario == null) return 0;

        ScenarioTree begin;
        ScenarioTree end;
        if (expansion == null) {
            begin = expansionBegins[0];
            end = finalScenario.Value;
        } else {
            begin = expansionBegins[expansion.Value.RowId];
            end = expansionEnds[expansion.Value.RowId];
        }

        var complete = current.Value.Unknown2 - (float)begin.Unknown2;
        var total = 1 + (end.Unknown2 - (float)begin.Unknown2);

        if (QuestManager.IsQuestComplete(current.Value.RowId)) complete += 1;
        return complete / total;
    }

    private ScenarioTree? GetCurrentScenarioTreeEntry() {
        var agent = AgentScenarioTree.Instance();
        if (agent == null) return null;
        if (agent->Data == null) return null;
        uint index = agent->Data->CompleteScenarioQuest;
        if (index == 0) {
            index = agent->Data->CurrentScenarioQuest;
        }

        if (index == 0) return null;
        var result = Service.Data.GetExcelSheet<ScenarioTree>().GetRow(index | 0x10000U);
        return result;
    }

    [AddonPostRefresh("ScenarioTree")]
    private void UpdateAddon(AtkUnitBase* addon) {
        if (addon == null) return;
        
        if (addon->AtkValuesCount < 8) return;
        var textValue = addon->AtkValues + 7;
        if (textValue->Type != ValueType.String || textValue->String == null) return;

        var button = addon->GetButtonNodeById(13);
        if (button == null) return;

        var textNode = (AtkTextNode*)button->AtkComponentBase.GetTextNodeById(6);
        if (textNode == null) return;

        var text = Common.ReadSeString(textValue->String);
        
        if (!Unloading) {
            var percentage = TweakConfig.UseCurrentExpansion ? GetScenarioCompletionForCurrentExpansion() : GetScenarioCompletion();
            var percentageString = new TextPayload(string.Format($" ({{0:P{Math.Clamp(TweakConfig.Accuracy, 0, 3)}}}) ", percentage));
            if (TweakConfig.ShowBeforeQuest) {
                text.Payloads.Insert(0, percentageString);
            } else {
                text.Payloads.Add(percentageString);
            }
        }

        var encoded = text.EncodeWithNullTerminator();
        if (encoded.Length == 0 || encoded[0] == 0) {
            SimpleLog.Verbose($"Update ScenarioTree: [empty string]");
            textNode->SetText(string.Empty);
        } else {
            SimpleLog.Verbose($"Update ScenarioTree: {text.TextValue}");
            textNode->SetText(encoded);
        }
    }
}
