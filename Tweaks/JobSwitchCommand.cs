
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace SimpleTweaksPlugin.Tweaks;

[TweakCategory(TweakCategory.Command)]
public unsafe class JobSwitchCommand : Tweak
{
  public override string Name => "Job Switch Command";
  public override string Description => "Adds commands to switch jobs using their acronyms. (Ex: /drk or /BLM)";
  private ExcelSheet<ClassJob>? classJobSheet;

  protected override void Enable()
  {
    base.Enable();

    classJobSheet = Service.Data.Excel.GetSheet<ClassJob>();
    if (classJobSheet == null)
    {
      SimpleLog.Error("ClassJob sheet is null");
      Ready = false;
      return;
    }

    classJobSheet.ToList().ForEach(row =>
    {
      var acronym = row.Abbreviation.ToString();
      var name = row.Name.ToString();
      var rId = row.RowId;
      if (!string.IsNullOrWhiteSpace(acronym) && !string.IsNullOrWhiteSpace(name) && rId != 0)
      {
        var lower = "/" + acronym.ToLowerInvariant();
        var upper = "/" + acronym.ToUpperInvariant();
        if (Service.Commands.Commands.ContainsKey(upper))
        {
          Plugin.Error(this, new Exception($"Command already exists: {upper}"));
        }
        else
        {
          Service.Commands.AddHandler(upper, new CommandInfo(OnCommand)
          {
            HelpMessage = $"Switches to {name} gearset.",
            ShowInHelp = false,
          });
        }
        if (Service.Commands.Commands.ContainsKey(lower))
        {
          Plugin.Error(this, new Exception($"Command already exists: {lower}"));
        }
        else
        {
          Service.Commands.AddHandler(lower, new CommandInfo(OnCommand)
          {
            HelpMessage = $"Switches to {name} gearset.",
            ShowInHelp = false,
          });
        }
      }
    });
  }

  protected override void Disable()
  {
    if (classJobSheet != null)
    {
      classJobSheet.ToList().ForEach(row =>
      {
        var acronym = row.Abbreviation.ToString();
        if (!string.IsNullOrWhiteSpace(acronym))
        {
          var lower = "/" + acronym.ToLowerInvariant();
          var upper = "/" + acronym.ToUpperInvariant();
          if (Service.Commands.Commands.ContainsKey(upper))
          {
            Service.Commands.RemoveHandler(upper);
          }
          if (Service.Commands.Commands.ContainsKey(lower))
          {
            Service.Commands.RemoveHandler(lower);
          }
        }
      });
    }
    base.Disable();
  }

  protected void OnCommand(string command, string arguments)
  {
    if (string.IsNullOrWhiteSpace(command))
    {
      Service.Chat.PrintError("JobSwitch: No command specified.");
      return;
    }
    else if (command.StartsWith("/"))
    {
      command = command.Substring(1);
    }

    var cj = classJobSheet!.ToList().FirstOrDefault(row => row.Abbreviation.ToString().Equals(command, StringComparison.InvariantCultureIgnoreCase));

    if (cj == null)
    {
      Service.Chat.PrintError($"JobSwitch: No class job found for command: {command}");
      return;
    }

    var gSet = GetGearsetForClassJob(cj);

    if (gSet == null)
    {
      Service.Chat.PrintError($"JobSwitch: No gearset found for class job: {cj.Name}");
      return;
    }

    ChatHelper.SendMessage($"/gearset change {gSet.Value + 1}");
  }

  // Shamelessly stolen from Tweaks/CharacterClassSwitcher.cs
  private byte? GetGearsetForClassJob(ClassJob cj)
  {
    byte? backup = null;
    var gearsetModule = RaptureGearsetModule.Instance();
    for (var i = 0; i < 100; i++)
    {
      var gearset = gearsetModule->GetGearset(i);
      if (gearset == null) continue;
      if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
      if (gearset->ID != i) continue;
      if (gearset->ClassJob == cj.RowId) return gearset->ID;
      if (backup == null && cj.ClassJobParent.Row != 0 && gearset->ClassJob == cj.ClassJobParent.Row) backup = gearset->ID;
    }

    return backup;
  }
}

