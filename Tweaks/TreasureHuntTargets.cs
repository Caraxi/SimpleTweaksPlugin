using System;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Block Targeting Treasure Hunt Enemies")]
[TweakDescription("Disable targeting for enemies that are part of another players Treasure Hunt duty.")]
[TweakTags("maps")]
[Changelog("1.8.5.0", "Fixed incorrect blocking targeting of Alexandrite Map targets.")]
public unsafe class TreasureHuntTargets : Tweak {
    [TweakHook(typeof(Character), nameof(Character.GetIsTargetable), nameof(IsTargetableDetour))]
    private HookWrapper<Character.Delegates.GetIsTargetable> isTargetableHook;

    private bool IsTargetableDetour(Character* potentialTarget) {
        var isTargetable = isTargetableHook.Original(potentialTarget);
        if (!isTargetable) return false;
        if (potentialTarget == null) return true;
        if (potentialTarget->ObjectKind != ObjectKind.BattleNpc) return true;
        if (potentialTarget->SubKind != 5) return true;
        if (potentialTarget->EventId.ContentId != EventHandlerContent.TreasureHuntDirector) return true;
        return potentialTarget->NamePlateIconId is 60094 or 60096;
    }
}
