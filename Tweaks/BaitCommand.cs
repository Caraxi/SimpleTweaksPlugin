using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Bait Command")]
[TweakDescription("Adds /$[CustomOrDefaultCommand] to switch fishing baits.")]
[Changelog("1.8.7.0", "Fixed tweak not enabling when starting the game.")]
public unsafe class BaitCommand : CommandTweak {
    protected override string HelpMessage => "Switches fishing baits.";
    protected override string Command => "bait";

    private static readonly Dictionary<string, uint> Bait = Service.Data.GetExcelSheet<Item>()!
        .Where(i => i.ItemSearchCategory.Row == 30)
        .ToDictionary(b => b.Name.ToString().ToLower(), b => b.RowId);
    
    private delegate byte ExecuteCommandDelegate(int id, int unk1, uint baitId, int unk2, int unk3);
    
    [Signature("E8 ?? ?? ?? ?? 8D 43 0A")]
    private ExecuteCommandDelegate executeCommand;

    protected override void OnCommand(string args) {
        if (!Bait.TryGetValue(args.ToLower(), out var bait) || bait == 0) {
            SimpleLog.Debug("invalid bait");
            Service.Chat.PrintError("Invalid Bait", Name);
            return;
        }

        var currentBait = UIState.Instance()->PlayerState.FishingBait;
        SimpleLog.Debug($"bait: {bait}, currentBait: {currentBait}");

        if (bait == currentBait) {
            SimpleLog.Debug("refusing to switch, same bait");
            return;
        }

        if (InventoryManager.Instance()->GetInventoryItemCount(bait) <= 0) {
            SimpleLog.Debug("refusing to switch, not enough bait");
            Service.Chat.PrintError("You have none of that bait.", Name);
            return;
        }
        
        var fishing = Service.Condition[ConditionFlag.Fishing];
        if (fishing) {
            SimpleLog.Debug("refusing to switch, fishing");
            Service.Chat.PrintError("You cannot currently change bait.", Name);
            return;
        }

        executeCommand(701, 4, bait, 0, 0);
    }
}
