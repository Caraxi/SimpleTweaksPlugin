using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Long Veil")]
[TweakDescription("Replaces the wedding veils with their long variants that are usually only shown in the sanctum of the twelve.")]
public unsafe class LongVeil : Tweak {
    [TweakHook, Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B DA 49 8B F0 48 8B F9", DetourName = nameof(FlagSlotForUpdateDetour))]
    private HookWrapper<Human.Delegates.FlagSlotForUpdate> flagSlotUpdateHook;

    private ulong FlagSlotForUpdateDetour(Human* a1, uint a2, EquipmentModelId* a3) {
        if (a2 == 0 && a3->Id == 208) a3->Id = 199;
        return flagSlotUpdateHook.Original(a1, a2, a3);
    }
}
