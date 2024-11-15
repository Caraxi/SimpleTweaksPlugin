using System.Linq;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Sync Crafter Bars")]
[TweakDescription("Keeps DoH job bars in sync")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
public class SyncCrafterBars : Tweak {
    public class Configs : TweakConfig {
        public bool[] StandardBars = new bool[10];
        public bool[] CrossBars = new bool[8];
    }

    [TweakConfig] public Configs Config { get; private set; }

    public bool IsShared(int number, bool cross) {
        return Service.GameConfig.UiConfig.TryGetBool($"Hotbar{(cross ? "Cross" : "")}Common{number + 1:00}", out var v) && v;
    }

    protected void DrawConfig() {
        if (Config.StandardBars.Length != 10) Config.StandardBars = new bool[10];
        if (Config.CrossBars.Length != 8) Config.CrossBars = new bool[8];

        ImGui.Text("Select bars to sync between jobs.");
        ImGui.Indent();

        var columns = (int)(ImGuiExt.GetWindowContentRegionSize().X / (150f * ImGui.GetIO().FontGlobalScale));

        ImGui.Columns(columns, "hotbarColumns", false);
        for (var i = 0; i < Config.StandardBars.Length; i++) {
            var isShared = IsShared(i, false);
            using (ImRaii.Disabled(isShared)) {
                ImGui.Checkbox($"Hotbar {i + 1}##syncBar_{i}", ref Config.StandardBars[i]);
            }

            if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip("Shared Hotbars will not be synced");
            }

            if (isShared && Config.StandardBars[i]) {
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("Shared Hotbars will not be synced");
                }
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.Columns(columns, "crosshotbarColumns", false);
        for (var i = 0; i < Config.CrossBars.Length; i++) {
            var isShared = IsShared(i, true);
            using (ImRaii.Disabled(isShared)) {
                ImGui.Checkbox($"Cross Hotbar {i + 1}##syncCrossBar_{i}", ref Config.CrossBars[i]);
            }

            if (isShared && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                ImGui.SetTooltip("Shared Cross Hotbars will not be synced");
            }

            if (isShared && Config.CrossBars[i]) {
                using (ImRaii.PushColor(ImGuiCol.TextDisabled, ImGuiColors.DalamudYellow)) {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("Shared Cross Hotbars will not be synced");
                }
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.Unindent();
    }

    private readonly uint[] crafterJobs = [8, 9, 10, 11, 12, 13, 14, 15];
    private uint currentClassJob;

    public void ExecuteCommand(string command) {
        SimpleLog.Log($"Execute Command: {command}");
        ChatHelper.SendMessage(command);
    }

    public void PerformCrafterBarSync(uint copyFrom) {
        if (Service.ClientState.LocalPlayer == null) return;
        if (Service.ClientState.IsPvP) return;
        if (!crafterJobs.Contains(copyFrom)) return;
        var classJobSheet = Service.Data.GetExcelSheet<ClassJob>();
        
        var copyJob = classJobSheet.GetRowOrNull(copyFrom);
        if (copyJob == null) return;

        foreach (var jobId in crafterJobs) {
            if (jobId == copyJob.Value.RowId) continue;
            var job = classJobSheet.GetRowOrNull(jobId);
            if (job == null) continue;

            for (var i = 0; i < 10; i++) {
                if (IsShared(i, false)) continue;
                if (Config.StandardBars[i]) {
                    ExecuteCommand($"/hotbar copy {copyJob.Value.Abbreviation.ExtractText()} {i + 1} {job.Value.Abbreviation.ExtractText()} {i + 1}");
                }
            }

            for (var i = 0; i < 8; i++) {
                if (IsShared(i, true)) continue;
                if (Config.CrossBars[i]) {
                    ExecuteCommand($"/crosshotbar copy {copyJob.Value.Abbreviation.ExtractText()} {i + 1} {job.Value.Abbreviation.ExtractText()} {i + 1}");
                }
            }
        }
    }

    [FrameworkUpdate(NthTick = 20)]
    private void FrameworkOnUpdate() {
        if (Service.ClientState.LocalPlayer == null) {
            currentClassJob = 0;
            return;
        }

        var cj = Service.ClientState.LocalPlayer.ClassJob.RowId;
        if (cj == currentClassJob) return;

        if (currentClassJob != 0 && crafterJobs.Contains(currentClassJob)) {
            SimpleLog.Debug($"Switched Job from {currentClassJob}");
            PerformCrafterBarSync(currentClassJob);
        }

        currentClassJob = cj;
    }
}
