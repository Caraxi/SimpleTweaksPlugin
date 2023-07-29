using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe partial class ColoredDutyRoulette : UiAdjustments.SubTweak
{
    public override string Name => "Color Duty Roulette Names";
    public override string Description => "Colors Duty Roulette names to indicate their completion status";
    protected override string Author => "MidoriKami";

    private AddonContentsFinder* Addon => (AddonContentsFinder*) Common.GetUnitBase("ContentsFinder");
    private AgentContentsFinder* Agent => AgentContentsFinder.Instance();
    private RouletteController* Controller => RouletteController.Instance();

    [GeneratedRegex("[^\\p{L}\\p{N}]")]
    private static partial Regex Alphanumeric();

    private readonly Dictionary<string, uint> rouletteIdDictionary = new();

    public class Config : TweakConfig
    {
        public bool ColorCompleteRoulette = true;
        public bool ColorIncompleteRoulette = true;
        
        public Vector4 IncompleteColor = new(1.0f, 0.0f, 0.0f, 1.0f);
        public Vector4 CompleteColor = new(0.0f, 1.0f, 0.0f, 1.0f);

        public List<uint> EnabledRoulettes = new();
    }
    
    private Config TweakConfig { get; set; } = null!;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
    {
        if (ImGui.Checkbox("Recolor Completed Roulettes", ref TweakConfig.ColorCompleteRoulette)) hasChanged = true;
        if (ImGui.Checkbox("Recolor Incomplete Roulettes", ref TweakConfig.ColorIncompleteRoulette)) hasChanged = true;
        if (ImGui.ColorEdit4("Completed Color", ref TweakConfig.CompleteColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf)) hasChanged = true;
        if (ImGui.ColorEdit4("Incomplete Color", ref TweakConfig.IncompleteColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf)) hasChanged = true;

        ImGuiHelpers.ScaledDummy(5.0f);
        
        ImGui.Text("Duty Selection");
        ImGui.Separator();

        var roulettes = Service.Data.GetExcelSheet<ContentRoulette>()!
            .Where(roulette => roulette.Name != string.Empty && roulette.DutyType != string.Empty)
            .OrderBy(roulette => roulette.SortKey);
        
        foreach (var roulette in roulettes)
        {
            var enabled = TweakConfig.EnabledRoulettes.Contains(roulette.RowId);
            if(ImGui.Checkbox(roulette.Name.ToDalamudString().ToString(), ref enabled))
            {
                if (TweakConfig.EnabledRoulettes.Contains(roulette.RowId) && !enabled)
                {
                    TweakConfig.EnabledRoulettes.Remove(roulette.RowId);
                    hasChanged = true;
                }
                else if (!TweakConfig.EnabledRoulettes.Contains(roulette.RowId) && enabled)
                {
                    TweakConfig.EnabledRoulettes.Add(roulette.RowId);
                    hasChanged = true;
                }
            }
        }
    };

    public override void RequestSaveConfig() => SaveConfig(TweakConfig);
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.5.0");
        AddChangelog("1.8.6.1", "Adds ability to select individual roulettes for recoloring.");

        foreach (var entry in Service.Data.GetExcelSheet<ContentRoulette>()!.Where(roulette => roulette.Name != string.Empty))
        {
            var filteredName = Alphanumeric().Replace(entry.Category.ToString().ToLower(), string.Empty);
            rouletteIdDictionary.TryAdd(filteredName, entry.RowId);
        }
        
        base.Setup();
    }

    protected override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    protected override void Disable()
    {
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        ResetAllNodes();
        base.Disable();
    }
    
    private void OnFrameworkUpdate()
    {
        if (!UiHelper.IsAddonReady((AtkUnitBase*)Addon)) return;
        if (!Agent->AgentInterface.IsAgentActive()) return;
        if (Addon->DutyList is null) return;
        
        foreach (uint nodeId in Enumerable.Range(61001, 15).Append(6))
        {
            var listComponentNode = Common.GetNodeByID<AtkComponentNode>(&Addon->DutyList->AtkComponentList.AtkComponentBase.UldManager, nodeId);
            if (listComponentNode is null || listComponentNode->Component is null) continue;

            var dutyNameNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 5);
            var dutyLevelNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 15);
            if (dutyNameNode is null || dutyLevelNode is null) continue;

            if (Agent->SelectedTab == 0)
            {
                var filteredDutyName = Alphanumeric().Replace(dutyNameNode->NodeText.ToString().ToLower(), string.Empty);

                if (!rouletteIdDictionary.TryGetValue(filteredDutyName, out var rouletteId))
                {
                    dutyNameNode->TextColor = dutyLevelNode->TextColor;
                    continue;
                }
            
                switch (Controller->IsRouletteComplete((byte) rouletteId))
                {
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
            else
            {
                dutyNameNode->TextColor = dutyLevelNode->TextColor;
            }
        }
    }

    private void ResetAllNodes()
    {
        if (!UiHelper.IsAddonReady((AtkUnitBase*)Addon)) return;
        if (!Agent->AgentInterface.IsAgentActive()) return;
        if (Addon->DutyList is null) return;
        
        foreach (uint nodeId in Enumerable.Range(61001, 15).Append(6))
        {
            var listComponentNode = Common.GetNodeByID<AtkComponentNode>(&Addon->DutyList->AtkComponentList.AtkComponentBase.UldManager, nodeId);
            if (listComponentNode is null || listComponentNode->Component is null) continue;

            var dutyNameNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 5);
            var dutyLevelNode = Common.GetNodeByID<AtkTextNode>(&listComponentNode->Component->UldManager, 15);
            if (dutyNameNode is null || dutyLevelNode is null) continue;
            
            dutyNameNode->TextColor = dutyLevelNode->TextColor;
        }
    }

    private void SetNodeColor(AtkTextNode* node, Vector4 color)
    {
        node->TextColor.R = (byte) (color.X * 255);
        node->TextColor.G = (byte) (color.Y * 255);
        node->TextColor.B = (byte) (color.Z * 255);
        node->TextColor.A = (byte) (color.W * 255);
    }
}