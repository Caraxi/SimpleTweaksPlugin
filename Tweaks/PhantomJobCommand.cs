using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Phantom Job Command")]
[TweakDescription("Adds a command to switch phantom jobs within Occult Crescent")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class PhantomJobCommand : CommandTweak {
    protected override string Command => "/phantomjob";

    protected override void OnCommand(string args) {
        if (GameMain.Instance()->CurrentTerritoryIntendedUseId != 61) {
            Service.Chat.PrintError("You can only use this command in Occult Crescent", "Simple Tweaks", 500);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(args)) {
            Service.Chat.PrintError($"/{CustomOrDefaultCommand} [job]", "Simple Tweaks", 500);
            return;
        }
        
        if (!uint.TryParse(args, out var id)) {
            id = uint.MaxValue;
            foreach (var pj in Service.Data.GetExcelSheet<MKDSupportJob>()) {
                if (!pj.Unknown0.ExtractText().Contains(args.Trim(), StringComparison.InvariantCultureIgnoreCase)) continue;
                id = pj.RowId;
                break;
            }

            if (id == uint.MaxValue) {
                Service.Chat.PrintError($"'{args}' is not a valid phantom job.", "Simple Tweaks", 500);
                return;
            }
        }

        if (!Service.Data.GetExcelSheet<MKDSupportJob>().HasRow(id)) {
            Service.Chat.PrintError($"'{args}' is not a valid phantom job.", "Simple Tweaks", 500);
            return;
        }

        Common.SendEvent(AgentId.MKDSupportJobList, 1, 0, id);
    }
}
