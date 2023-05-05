using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks;

// stolen from gatherbuddy lol
// https://github.com/Ottermandias/GatherBuddy/blob/b03c8fdab0392c6e67d7ea55ea71df879e6d46be/GatherBuddy/SeFunctions/CurrentBait.cs
public unsafe class BaitCommand : CommandTweak {
    public override string Name => "Bait Command";
    public override string Description => $"Adds /{Command} to switch fishing baits.";
    protected override string HelpMessage => "Switches fishing baits.";
    protected override string Command => "bait";

    private static Dictionary<string, uint> Bait = Service.Data.GetExcelSheet<Item>()
        .Where(i => i.ItemSearchCategory.Row == 30)
        .ToDictionary(b => b.Name.ToString().ToLower(), b => b.RowId);

    private IntPtr currentBaitAddress;
    
    private delegate byte ExecuteCommandDelegate(int id, int unk1, uint baitId, int unk2, int unk3);
    private ExecuteCommandDelegate executeCommand;
    
    public override void Setup() {
        base.Setup();
        AddChangelog("1.8.7.0", "Fixed tweak not enabling when starting the game.");
    }

    public override void Enable() {
        currentBaitAddress =
            Service.SigScanner.GetStaticAddressFromSig("48 83 C4 30 5B C3 49 8B C8 E8 ?? ?? ?? ?? 3B 05");
        if (executeCommand is null) {
            var executeCommandPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 8D 43 0A");
            executeCommand = Marshal.GetDelegateForFunctionPointer<ExecuteCommandDelegate>(executeCommandPtr);
        }
        base.Enable();
    }

    public static int HasItem(uint itemID)
        => InventoryManager.Instance()->GetInventoryItemCount(itemID);

    protected override void OnCommand(string args) {
        uint bait;
        if (!Bait.TryGetValue(args.ToLower(), out bait)) {
            SimpleLog.Debug("invalid bait");
            return;
        }

        var currentBait = *(uint*)currentBaitAddress;
        SimpleLog.Debug($"bait: {bait}, currentBait: {currentBait}");

        if (bait == currentBait) {
            SimpleLog.Debug("refusing to switch, same bait");
            return;
        }

        if (bait == 0) {
            SimpleLog.Debug("refusing to switch, invalid bait");
            return;
        }

        if (HasItem(bait) <= 0) {
            SimpleLog.Debug("refusing to switch, not enough bait");
            return;
        }
        
        var fishing = Service.Condition[ConditionFlag.Fishing];
        if (fishing) {
            SimpleLog.Debug("refusing to switch, fishing");
            return;
        }

        executeCommand(701, 4, bait, 0, 0);
    }
}
