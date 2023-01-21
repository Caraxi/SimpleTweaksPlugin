﻿using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class TreasureHuntTargets : Tweak {
    public override string Name => "Block Targeting Treasure Hunt Enemies";
    public override string Description => "Disable targeting for enemies that are part of another players Treasure Hunt duty.";
    public override bool Experimental => true;
    
    private delegate bool GetIsTargetable(GameObject* character);
    private HookWrapper<GetIsTargetable> isTargetableHook;

    public override void Enable() {
        isTargetableHook ??= Common.Hook<GetIsTargetable>("40 53 48 83 EC 20 F3 0F 10 89 ?? ?? ?? ?? 0F 57 C0 0F 2E C8 48 8B D9 7A 0A", IsTargetableDetour);
        isTargetableHook?.Enable();
        
        base.Enable();
    }

    private bool IsTargetableDetour(GameObject* potentialTarget) {
        var isTargetable = isTargetableHook.Original(potentialTarget);
        if (!isTargetable) 
            return false;
        if (potentialTarget->ObjectKind != 2) 
            return true;
        if (potentialTarget->EventId.Type != EventHandlerType.TreasureHuntDirector) 
            return true;

        return potentialTarget->NamePlateIconId == 60094;
    }

    public override void Disable() {
        isTargetableHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        isTargetableHook?.Dispose();
        base.Dispose();
    }
}