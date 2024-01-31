using System;
using System.Linq;
using System.Numerics;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakAuthor("Lumina Sapphira")]
public unsafe class EquipJobCommand : CommandTweak
{
    public override string Name => "Equip Job Command";
    public override string Description => "Adds a command to switch to a class or job's gearset.";
    protected override string Command => "equipjob";
    protected override string HelpMessage => "Switches to the highest item-level gearset for a job.";

    private class Config : TweakConfig
    {
        public bool AllowPriority;
    }

    private Config TweakConfig { get; set; } = null!;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    { 
        if (ImGui.Checkbox(LocString("PriorityName", "Allow priority list of jobs? (Only allows using abbreviations)", "Allow Priority Config Option"), ref TweakConfig.AllowPriority))
        {
            SaveConfig(TweakConfig);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetNextWindowSize(new Vector2(280, -1));
            ImGui.BeginTooltip();
            ImGui.TextWrapped(LocString("PriorityHelp", "Useful when generalizing between classes / jobs (e.g. /equipjob pld gla)", "Allow Priority Tooltip"));
            ImGui.EndTooltip();
        }
        ImGui.Separator();
        ImGui.Text($"/{Command}");
    };

    protected override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        base.Enable();
    }

    protected override void Disable()
    {
        SaveConfig(TweakConfig);
        base.Disable();
    }

    protected override void OnCommand(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            if (TweakConfig.AllowPriority)
                Service.Chat.PrintError($"/{Command} (priority list of job abbreviations...)");
            else
                Service.Chat.PrintError($"/{Command} (job abbreviation / name)");
            return;
        }

        if (TweakConfig.AllowPriority)
        {
            var options = arguments.Split(" ");
            foreach (var option in options)
            {
                switch (TrySwitchClassJob(option, true)) 
                {
                    case SwitchClassJobResult.Success:
                        return;
                    case SwitchClassJobResult.FailedToFindGearset:
                        continue;
                    case SwitchClassJobResult.FailedToFindClassJob:
                        Service.Chat.PrintError($"Bad job/class abbreviation: \"{option}\"");
                        continue;
                    case SwitchClassJobResult.InternalError:
                        Service.Chat.PrintError("An error occurred.");
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        else
        {
            switch (TrySwitchClassJob(arguments, false))
            {
                case SwitchClassJobResult.Success:
                    break;
                case SwitchClassJobResult.FailedToFindGearset:
                    Service.Chat.PrintError("No suitable gearset found");
                    break;
                case SwitchClassJobResult.FailedToFindClassJob:
                    Service.Chat.PrintError($"Unable to find job {arguments}");
                    break;
                case SwitchClassJobResult.InternalError:
                    Service.Chat.PrintError("An error occurred.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
    }

    private enum SwitchClassJobResult
    {
        Success,
        FailedToFindGearset,
        FailedToFindClassJob,
        InternalError
    }

    private static SwitchClassJobResult TrySwitchClassJob(string arg, bool abbreviationsOnly)
    {
        bool ComparisonDelegate(ClassJob row)
        {
            var abbr = row.Abbreviation.ToDalamudString().ToString().ToLower().Equals(arg.ToLower());
            if (abbreviationsOnly || abbr) return abbr;
            return row.Name.ToDalamudString().ToString().ToLower().Equals(arg.ToLower());
        }

        var sheet = Service.Data.GetExcelSheet<ClassJob>();
        var classJob =
            from row in sheet
            where ComparisonDelegate(row)
            select row.RowId;
        var id = classJob.FirstOrDefault((uint)0);
        return id == 0 ?
            SwitchClassJobResult.FailedToFindClassJob : SwitchClassJobID(id);
    }

    private static SwitchClassJobResult SwitchClassJobID(uint classJobId)
    {
        var raptureGearsetModule = RaptureGearsetModule.Instance();
        if (raptureGearsetModule == null)
            return SwitchClassJobResult.InternalError;

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
        if (bestGearset.ClassJob == 0) return SwitchClassJobResult.FailedToFindGearset;
        raptureGearsetModule->EquipGearset(bestGearset.Id);
        Service.Chat.Print($"Equipping gearset #{bestGearset.Id + 1}");
        return SwitchClassJobResult.Success;
    }
}
