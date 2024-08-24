using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Leveling Dungeon Command")]
[TweakDescription("Adds a command to open the highest level leveling dungeon available for your level.")]
[TweakReleaseVersion("0.0.0.0")]
[TweakAuthor("LuminaSapphira")]
public class LevelingDungeonCommand : CommandTweak
{
    protected override string Command => "/levelingdungeon";

    protected override string HelpMessage => "Open the highest level leveling dungeon.";
    
    protected override unsafe void OnCommand(string args)
    {
        if (Service.Condition.Cutscene()) {
            Service.Chat.PrintError("You cannot open the Duty Finder during a cutscene.");
            return;
        }

        if (Service.Condition.Duty()) {
            Service.Chat.PrintError("You cannot open the Duty Finder while in a duty.");
            return;
        }

        var sheet = Service.Data.GetExcelSheet<ContentFinderCondition>();
        var row = sheet?
            .Where(row => row.ContentType.Row == (uint)InstanceContentType.Dungeon)
            .Where(row => UIState.IsInstanceContentUnlocked(row.Content))
            .Where(row => row.ClassJobLevelRequired <= UIState.Instance()->PlayerState.CurrentLevel)
            .Where(row => row.ClassJobLevelRequired % 10 != 0) // Only leveling dungeons
            .MaxBy(row => row.ClassJobLevelRequired);
        var id = row?.RowId;

        if (id.HasValue)
            AgentContentsFinder.Instance()->OpenRegularDuty(id.Value);
        else
            Service.Chat.PrintError("Failed to find a valid leveling dungeon.");
    }
}