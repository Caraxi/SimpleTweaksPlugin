using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Color Duty Roulette Names")]
[TweakDescription("Colors Duty Roulette names to indicate their completion status")]
[TweakAuthor("MidoriKami")]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.5.0")]
[Changelog("1.8.6.1", "Adds ability to select individual roulettes for recoloring.")]
public unsafe partial class ColoredDutyRoulette : UiAdjustments.SubTweak {

    [GeneratedRegex(@"[^\p{L}\p{N}]")]
    private static partial Regex Alphanumeric();

    private readonly Dictionary<string, uint> rouletteIdDictionary = new();

    public class Config : TweakConfig {
        public bool ColorCompleteRoulette = true;
        public bool ColorIncompleteRoulette = true;
        
        public Vector4 IncompleteColor = new(1.0f, 0.0f, 0.0f, 1.0f);
        public Vector4 CompleteColor = new(0.0f, 1.0f, 0.0f, 1.0f);

        public List<uint> EnabledRoulettes = new();
    }
    
    private Config TweakConfig { get; set; } = null!;

    private void DrawConfig() {
        if (ImGui.Checkbox("Recolor Completed Roulettes", ref TweakConfig.ColorCompleteRoulette)) SaveConfig(TweakConfig);
        if (ImGui.Checkbox("Recolor Incomplete Roulettes", ref TweakConfig.ColorIncompleteRoulette)) SaveConfig(TweakConfig);
        if (ImGui.ColorEdit4("Completed Color", ref TweakConfig.CompleteColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf)) SaveConfig(TweakConfig);
        if (ImGui.ColorEdit4("Incomplete Color", ref TweakConfig.IncompleteColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf)) SaveConfig(TweakConfig);

        ImGuiHelpers.ScaledDummy(5.0f);
        
        ImGui.Text("Duty Selection");
        ImGui.Separator();

        var roulettes = Service.Data.GetExcelSheet<ContentRoulette>()!
            .Where(roulette => roulette.Name != string.Empty && roulette.DutyType != string.Empty)
            .OrderBy(roulette => roulette.SortKey);
        
        foreach (var roulette in roulettes) {
            var enabled = TweakConfig.EnabledRoulettes.Contains(roulette.RowId);
            if(ImGui.Checkbox(roulette.Name.ToDalamudString().ToString(), ref enabled)) {
                if (TweakConfig.EnabledRoulettes.Contains(roulette.RowId) && !enabled) {
                    TweakConfig.EnabledRoulettes.Remove(roulette.RowId);
                    SaveConfig(TweakConfig);
                }
                else if (!TweakConfig.EnabledRoulettes.Contains(roulette.RowId) && enabled) {
                    TweakConfig.EnabledRoulettes.Add(roulette.RowId);
                    SaveConfig(TweakConfig);
                }
            }
        }
    }
    
    public override void Setup() {
        foreach (var entry in Service.Data.GetExcelSheet<ContentRoulette>()!.Where(roulette => roulette.Name != string.Empty)) {
            var filteredName = Alphanumeric().Replace(entry.Category.ToString().ToLower(), string.Empty);
            rouletteIdDictionary.TryAdd(filteredName, entry.RowId);
        }
    }

    protected override void Disable() {
        ResetAllNodes();
    }
    
    [AddonPostRefresh("ContentsFinder")]
    private void OnContentsFinderRefresh(AddonRefreshArgs args) {
        var addon = (AddonContentsFinder*) args.Addon;
        
        foreach (uint nodeId in Enumerable.Range(61001, 15).Append(6)) {
            var listComponentNode = Common.GetNodeByID<AtkComponentNode>(&addon->DutyList->AtkComponentList.AtkComponentBase.UldManager, nodeId);
            if (listComponentNode is null || listComponentNode->Component is null) continue;

            var dutyNameNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 5);
            var dutyLevelNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 15);
            if (dutyNameNode is null || dutyLevelNode is null) continue;

            if (AgentContentsFinder.Instance()->SelectedTab is 0) {
                var filteredDutyName = Alphanumeric().Replace(dutyNameNode->NodeText.ToString().ToLower(), string.Empty);

                if (!rouletteIdDictionary.TryGetValue(filteredDutyName, out var rouletteId)) {
                    dutyNameNode->TextColor = dutyLevelNode->TextColor;
                    continue;
                }
            
                switch (RouletteController.Instance()->IsRouletteComplete((byte) rouletteId)) {
                    case true when TweakConfig.ColorCompleteRoulette && TweakConfig.EnabledRoulettes.Contains(rouletteId):
                        SetNodeColor(dutyNameNode, TweakConfig.CompleteColor);
                        break;
                
                    case false when TweakConfig.ColorIncompleteRoulette && TweakConfig.EnabledRoulettes.Contains(rouletteId):
                        SetNodeColor(dutyNameNode, TweakConfig.IncompleteColor);
                        break;
                
                    default:
                        dutyNameNode->TextColor = dutyLevelNode->TextColor;
                        break;
                }
            }
            else {
                dutyNameNode->TextColor = dutyLevelNode->TextColor;
            }
        }
    }

    private void ResetAllNodes() {
        var addon = (AddonContentsFinder*)Common.GetUnitBase("ContentsFinder");

        if (!UiHelper.IsAddonReady((AtkUnitBase*)addon)) return;
        if (addon->DutyList is null) return;
        
        foreach (uint nodeId in Enumerable.Range(61001, 15).Append(6)) {
            var listComponentNode = Common.GetNodeByID<AtkComponentNode>(&addon->DutyList->AtkComponentList.AtkComponentBase.UldManager, nodeId);
            if (listComponentNode is null || listComponentNode->Component is null) continue;

            var dutyNameNode = (AtkTextNode*)listComponentNode->Component->GetTextNodeById(5);
            var dutyLevelNode = (AtkTextNode*)listComponentNode->Component->GetTextNodeById(15);
            if (dutyNameNode is null || dutyLevelNode is null) continue;
            
            dutyNameNode->TextColor = dutyLevelNode->TextColor;
        }
    }

    private void SetNodeColor(AtkTextNode* node, Vector4 color) {
        node->TextColor.R = (byte) (color.X * 255);
        node->TextColor.G = (byte) (color.Y * 255);
        node->TextColor.B = (byte) (color.Z * 255);
        node->TextColor.A = (byte) (color.W * 255);
    }
}