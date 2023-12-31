using System.Linq;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class EquipJobCommand : CommandTweak
{
    public override string Name => "Equip Job Command";
    public override string Description => "Adds a command to switch to a job's gearset.";
    protected override string Command => "equipjob";
    protected override string HelpMessage => "Switches to the highest item-level gearset for a job.";
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => ImGui.Text($"/{Command}");

    protected override void OnCommand(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            Service.Chat.PrintError($"/{Command} (job abbreviation)");
            return;
        }

        var sheet = Service.Data.GetExcelSheet<ClassJob>();
        var classJob =
            from row in sheet
            where row.Abbreviation.ToDalamudString().ToString().ToLower() == arguments.ToLower()
            select row.RowId;
        var id = classJob.FirstOrDefault((uint)0);
        if (id == 0)
            Service.Chat.PrintError($"Unable to find job {arguments}");
        else
            SwitchClassJob(id);
    }

    private void SwitchClassJob(uint classJobId)
    {
        var raptureGearsetModule = RaptureGearsetModule.Instance();
        if (raptureGearsetModule == null)
            return;

        var bestGearset = Enumerable.Range(0, 100)
            .Where(raptureGearsetModule->IsValidGearset)
            .Select(gsid =>
            {
                var gearset = raptureGearsetModule->GetGearset(gsid);
                return (Id: gearset->ID, ClassJob: gearset->ClassJob, ILvl: gearset->ItemLevel);
            })
            .Where(x => x.ClassJob == classJobId)
            .OrderByDescending(x => x.ILvl)
            .FirstOrDefault();
        if (bestGearset.ClassJob != 0)
        {
            Service.Chat.Print($"Equipping gearset #{bestGearset.Id + 1}");
            raptureGearsetModule->EquipGearset(bestGearset.Id);
        }
        else
        {
            Service.Chat.PrintError("No suitable gearset found");
        }
    }
}
