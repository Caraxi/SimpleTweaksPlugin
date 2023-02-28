﻿using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class OldNameplatesTweak : UiAdjustments.SubTweak {
    public override string Name => "Old Nameplates Tweak";
    public override string Description => "Reverts the change to nameplates.";
    protected override string Author => "aers";

    private delegate void AddonNameplateOnUpdateDelegate(AddonNamePlate* thisPtr, NumberArrayData** numberData,
        StringArrayData** stringData);
    private HookWrapper<AddonNameplateOnUpdateDelegate> addonNameplateOnUpdateHook;

    public override void Enable() {
        addonNameplateOnUpdateHook ??= Common.Hook(Service.SigScanner.ScanText("48 8B C4 41 56 48 81 EC ?? ?? ?? ?? 48 89 58 F0"), new AddonNameplateOnUpdateDelegate(AddonNameplateOnUpdateDetour));
        addonNameplateOnUpdateHook?.Enable();
        base.Enable();
    }
        
    public override void Disable() {
        addonNameplateOnUpdateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        addonNameplateOnUpdateHook?.Dispose();
        base.Dispose();
    }

    private void AddonNameplateOnUpdateDetour(AddonNamePlate* thisPtr, NumberArrayData** numberData,
        StringArrayData** stringData) {
        numberData[5]->IntArray[3] = 1;
        this.addonNameplateOnUpdateHook.Original(thisPtr, numberData, stringData);
    }
}