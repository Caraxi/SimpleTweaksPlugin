using System;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class HideExperienceBar : UiAdjustments.SubTweak {
    public override string Name => "Hide Experience Bar at Max Level";
    public override string Description => "Hides the experience bar when at max level.";
    protected override string Author => "Anna";

    private delegate void* AddonExpOnUpdateDelegate(AtkUnitBase* addonExp, NumberArrayData** numberArrayData, StringArrayData** stringArrayData, void* a4);
    private HookWrapper<AddonExpOnUpdateDelegate> addonExpOnUpdateHook;
    public override void Enable() {
        addonExpOnUpdateHook ??= Common.Hook<AddonExpOnUpdateDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 30 48 8B 72 18", AddonExpOnUpdateDetour);
        addonExpOnUpdateHook?.Enable();
        base.Enable();
    }

    private void* AddonExpOnUpdateDetour(AtkUnitBase* addonExp, NumberArrayData** numberArrays, StringArrayData** stringArrays, void* a4) {
        var ret =  addonExpOnUpdateHook.Original(addonExp, numberArrays, stringArrays, a4);
        var stringArray = stringArrays[2];
        if (stringArray == null) goto Return;
        var strPtr = stringArray->StringArray[69];
        if (strPtr == null) goto Return;

        try {
            var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(strPtr));
            SetExperienceBarVisible(!str.TextValue.Contains("-/-"));
        } catch {
            // Ignored
        }

        Return:
        return ret;
    }

    private static unsafe void SetExperienceBarVisible(bool visible) {
        var expAddon = Service.GameGui.GetAddonByName("_Exp", 1);
        if (expAddon == IntPtr.Zero) {
            return;
        }

        var addon = (AtkUnitBase*) expAddon;
        addon->IsVisible = visible;
    }

    public override void Disable() {
        SetExperienceBarVisible(true);
        addonExpOnUpdateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        addonExpOnUpdateHook?.Dispose();
        base.Dispose();
    }
}