using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class MinionAway : CommandTweak {
    public override string Name => "Dismiss Minion Command";
    public override string Description => $"Adds a command to dismiss your current minion. /{Command}";

    public override void Setup() {
        AddChangelogNewTweak("1.8.2.0");
        base.Setup();
    }

    protected override void OnCommand(string args) {
        var c = (Character*)(Service.ClientState.LocalPlayer?.Address ?? IntPtr.Zero);
        if (c == null) return;
        var minion = c->Companion.CompanionObject;
        if (minion == null) return;
        if (minion->Character.GameObject.DataID == 0) return;
        ActionManager.Instance()->UseAction((ActionType) 8, minion->Character.GameObject.DataID);
    }

    protected override string Command => "minionaway";
}
