using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Scenario Progression Display")]
[TweakDescription("Shows the percentage of completion of the main scenario.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.0.0")]
public unsafe class ScenarioProgressionDisplay : UiAdjustments.SubTweak {
    // TODO: Remove this when ClientStructs is updated.
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    public struct AgentScenarioTree {
        [FieldOffset(0x00)] public AgentInterface AgentInterface;
        [FieldOffset(0x28)] public AgentScenarioTreeData* Data;
        
        [StructLayout(LayoutKind.Explicit, Size = 0x30)]
        public struct AgentScenarioTreeData {
            [FieldOffset(0x00)] public ushort CurrentScenarioQuest;
            [FieldOffset(0x06)] public ushort CompleteScenarioQuest; // Only populated if no MSQ is accepted
        }
    }

    private ScenarioTree finalScenario;
    private readonly Dictionary<uint, ScenarioTree> expansionBegins = new();
    private readonly Dictionary<uint, ScenarioTree> expansionEnds = new();

    public class Config : TweakConfig {
        [TweakConfigOption("Show for current expansion", 1)]
        public bool UseCurrentExpansion = false;

        [TweakConfigOption("Percentage Accuracy", 2, IntMin = 0, IntMax = 3, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EnforcedLimit = true, EditorSize = 100)]
        public int Accuracy = 1;
    }

    public Config TweakConfig { get; private set; }

    protected override void Enable() => UpdateAddon(Common.GetUnitBase("ScenarioTree"));
    protected override void Disable() => UpdateAddon(Common.GetUnitBase("ScenarioTree"), true);
    protected override void ConfigChanged() => UpdateAddon(Common.GetUnitBase("ScenarioTree"));

    private float GetScenarioCompletionForCurrentExpansion() {
        var current = GetCurrentScenarioTreeEntry();
        if (current == null) return 0;
        var quest = Service.Data.GetExcelSheet<Quest>()?.GetRow(current.RowId);
        if (quest == null) return 0;
        return GetScenarioCompletion(quest.Expansion.Value);
    }

    private float GetScenarioCompletion(ExVersion expansion = null) {
        var current = GetCurrentScenarioTreeEntry();
        if (current == null) return 0;
        
        if (finalScenario == null) {
            foreach (var st in Service.Data.GetExcelSheet<ScenarioTree>()!) {
                var quest = Service.Data.GetExcelSheet<Quest>()?.GetRow(st.RowId);
                if (quest?.Expansion.Value == null) continue;

                if (finalScenario == null || st.Unknown1 > finalScenario.Unknown1) finalScenario = st;
                if (!expansionEnds.ContainsKey(quest.Expansion.Row)) expansionEnds.Add(quest.Expansion.Row, st);
                if (!expansionBegins.ContainsKey(quest.Expansion.Row)) expansionBegins.Add(quest.Expansion.Row, st);

                if (st.Unknown1 > expansionEnds[quest.Expansion.Row].Unknown1) {
                    expansionEnds[quest.Expansion.Row] = st;
                }

                if (st.Unknown1 < expansionBegins[quest.Expansion.Row].Unknown1) {
                    expansionBegins[quest.Expansion.Row] = st;
                }
            }
        }

        ScenarioTree begin;
        ScenarioTree end;
        if (expansion == null) {
            begin = expansionBegins[0];
            end = finalScenario;
        } else {
            begin = expansionBegins[expansion.RowId];
            end = expansionEnds[expansion.RowId];
        }

        var complete = current.Unknown1 - (float)begin.Unknown1;
        var total = 1 + (end.Unknown1 - (float)begin.Unknown1);

        if (QuestManager.IsQuestComplete(current.RowId)) complete += 1;
        return complete / total;
    }

    private ScenarioTree GetCurrentScenarioTreeEntry() {
        var agent = (AgentScenarioTree*)AgentModule.Instance()->GetAgentByInternalId(AgentId.ScenarioTree);
        if (agent == null) return null;
        if (agent->Data == null) return null;
        uint index = agent->Data->CompleteScenarioQuest;
        if (index == 0) {
            index = agent->Data->CurrentScenarioQuest;
        }

        if (index == 0) return null;
        var result = Service.Data.GetExcelSheet<ScenarioTree>()?.GetRow(index | 0x10000U);
        return result;
    }

    private delegate void* RefreshAddon(AtkUnitBase* addon, void* a2, void* a3);

    [TweakHook, Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 49 8B F8 8B F2 48 8B D9 85 D2 0F 84 ?? ?? ?? ?? 4D 85 C0 0F 84 ?? ?? ?? ?? 49 8B C8 E8 ?? ?? ?? ?? 85 C0 0F 88", DetourName = nameof(RefreshDetour))]
    private HookWrapper<RefreshAddon> refreshAddonHook;

    private void UpdateAddon(AtkUnitBase* addon = null, bool cleanup = false) {
        if (addon == null) return;
        
        if (addon->AtkValuesCount < 8) return;
        var textValue = addon->AtkValues + 7;
        if (textValue->Type != ValueType.String || textValue->String == null) return;

        var button = addon->GetButtonNodeById(13);
        if (button == null) return;

        var textNode = (AtkTextNode*)button->AtkComponentBase.GetTextNodeById(6);
        if (textNode == null) return;

        var text = Common.ReadSeString(textValue->String);
        
        if (!cleanup) {
            var percentage = TweakConfig.UseCurrentExpansion ? GetScenarioCompletionForCurrentExpansion() : GetScenarioCompletion();
            text.Append(string.Format($" ({{0:P{Math.Clamp(TweakConfig.Accuracy, 0, 3)}}})", percentage));
        }

        var encoded = text.Encode();
        if (encoded.Length == 0 || encoded[0] == 0) {
            SimpleLog.Verbose($"Update ScenarioTree: [empty string]");
            textNode->SetText(string.Empty);
        } else {
            SimpleLog.Verbose($"Update ScenarioTree: {text.TextValue}");
            textNode->SetText(encoded);
        }
    }
    
    private void* RefreshDetour(AtkUnitBase* addon, void* a2, void* a3) {
        var o = refreshAddonHook.Original(addon, a2, a3);
        UpdateAddon(addon);
        return o;
    }
}
