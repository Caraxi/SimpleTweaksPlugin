using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace SimpleTweaksPlugin; 

public static unsafe class Fools {
    public static bool IsFoolsDay => DateTime.Now is { Month: 4, Day: 1 };
    private static readonly ushort[] statuses = { 3, 227, 937 };
    
    public static void FrameworkUpdate() {
        if (!IsFoolsDay) return;
        if (Service.Condition[ConditionFlag.OccupiedInQuestEvent]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene]) return;
        if (Service.Condition[ConditionFlag.WatchingCutscene78]) return;
        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;
        for (var i = 200; i < 243; i++) {
            if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
            var o = (BattleChara*) GameObjectManager.GetGameObjectByIndex(i);
            if (o == null) continue;
            if (statuses.Any(s => o->StatusManager.HasStatus(s))) continue;
            var r = (ushort)new Random().Next(0, 100);
            if (r >= statuses.Length) r = (ushort)(r % 2);
            o->StatusManager.AddStatus(statuses[r % statuses.Length]);
        }
    }

    public static void Reset() {
        for (var i = 200; i < 243; i++) {
            if (i < 240 && Service.ClientState.LocalContentId != 0) continue;
            var o = (BattleChara*) GameObjectManager.GetGameObjectByIndex(i);
            if (o == null) continue;
            foreach (var s in statuses) {
                if (o->StatusManager.HasStatus(s)) {
                    o->StatusManager.RemoveStatus(s);
                }
            }
        }
    }
}
