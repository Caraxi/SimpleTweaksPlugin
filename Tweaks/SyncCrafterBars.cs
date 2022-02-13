using System.Linq;
using Dalamud.Game;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class SyncCrafterBars : Tweak {
    public override string Name => "Sync Crafter Bars";
    public override string Description => "Keeps DoH job bars in sync";

    public class Configs : TweakConfig {
        public bool[] StandardBars = new bool[10];
        public bool[] CrossBars = new bool[8];
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        if (Config.StandardBars.Length != 10) Config.StandardBars = new bool[10];
        if (Config.CrossBars.Length != 8) Config.CrossBars = new bool[8];

        ImGui.Text("Select bars to sync between jobs.");
        ImGui.Indent();

        var columns = (int) (ImGui.GetWindowContentRegionWidth() / (150f * ImGui.GetIO().FontGlobalScale));

        ImGui.Columns(columns, "hotbarColumns", false);
        for (var i = 0; i < Config.StandardBars.Length; i++) {
            ImGui.Checkbox($"Hotbar {i+1}##syncBar_{i}", ref Config.StandardBars[i]);
            ImGui.NextColumn();
        }
        ImGui.Columns(1);
        ImGui.Columns(columns, "crosshotbarColumns", false);
        for (var i = 0; i < Config.CrossBars.Length; i++) {
            ImGui.Checkbox($"Cross Hotbar {i+1}##syncCrossBar_{i}", ref Config.CrossBars[i]);
            ImGui.NextColumn();
        }
        ImGui.Columns(1);
        ImGui.Unindent();
    };

    private readonly uint[] crafterJobs = { 8, 9, 10, 11, 12, 13, 14, 15 };
    private uint currentClassJob;
    private ExcelSheet<ClassJob> classJobSheet;

    public void ExecuteCommand(string command) {
        SimpleLog.Log($"Execute Command: {command}");
        Plugin.XivCommon.Functions.Chat.SendMessage(command);
    }

    public void PerformCrafterBarSync(uint copyFrom) {
        if (Service.ClientState.LocalPlayer == null) return;
        if (!crafterJobs.Contains(copyFrom)) return;
        var copyJob = classJobSheet.GetRow(copyFrom);
        if (copyJob == null) return;

        foreach (var jobId in crafterJobs) {
            if (jobId == copyJob.RowId) continue;
            var job = classJobSheet.GetRow(jobId);
            if (job == null) continue;

            for (var i = 0; i < 10; i++) {
                if (Config.StandardBars[i]) {
                    ExecuteCommand($"/hotbar copy {copyJob.Abbreviation.RawString} {i+1} {job.Abbreviation.RawString} {i+1}");
                }
            }
            for (var i = 0; i < 8; i++) {
                if (Config.CrossBars[i]) {
                    ExecuteCommand($"/crosshotbar copy {copyJob.Abbreviation.RawString} {i+1} {job.Abbreviation.RawString} {i+1}");
                }
            }
        }
    }

    public override void Enable() {
        classJobSheet = Service.Data.Excel.GetSheet<ClassJob>();
        if (classJobSheet == null) {
            SimpleLog.Error("ClassJob sheet is null");
            Ready = false;
            return;
        }

        Service.Framework.Update += FrameworkOnUpdate;

        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    private byte t = 0;

    private void FrameworkOnUpdate(Framework framework) {
        if (t++ < 20) return;
        t = 0;

        if (Service.ClientState.LocalPlayer == null) {
            currentClassJob = 0;
            return;
        }

        var cj = Service.ClientState.LocalPlayer.ClassJob.Id;
        if (cj == currentClassJob) return;

        if (currentClassJob != 0 && crafterJobs.Contains(currentClassJob)) {
            SimpleLog.Log($"Switched Job from {currentClassJob}");
            PerformCrafterBarSync(currentClassJob);
        }

        currentClassJob = cj;
    }

    public override void Disable() {
        Service.Framework.Update -= FrameworkOnUpdate;
        SaveConfig(Config);
        base.Disable();
    }
}