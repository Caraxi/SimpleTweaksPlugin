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
    [TweakHook, Signature("40 53 48 83 EC 20 F3 0F 10 89 ?? ?? ?? ?? 0F 57 C0 0F 2E C8 48 8B D9 7A 02", DetourName = nameof(IsTargetableDetour))]
    private HookWrapper<Character.Delegates.GetIsTargetable> isTargetableHook;

    private bool IsTargetableDetour(Character* potentialTarget) {
        var isTargetable = isTargetableHook.Original(potentialTarget);
        if (!isTargetable) return false;
        if (potentialTarget == null) return true;
        if (potentialTarget->ObjectKind != ObjectKind.BattleNpc) return true;
        if (potentialTarget->SubKind != 5) return true;
        if (potentialTarget->EventId.ContentId != EventHandlerType.TreasureHuntDirector) return true;
        return potentialTarget->NamePlateIconId is 60094 or 60096;
    }
}
