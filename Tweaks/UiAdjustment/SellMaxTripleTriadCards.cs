using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class SellMaxTripleTriadCards : UiAdjustments.SubTweak {
    public override string Name => "Default to max when selling Triple Triad Cards";
    public override string Description => "Set the default number of cards to sell at the Triple Triad Trader to the number of cards you have.";

    private delegate void* AddonCardShopDialogOnSetup(AddonShopCardDialog* addonShopCardDialog, uint a2, void* a3);
    private HookWrapper<AddonCardShopDialogOnSetup> setupHook;

    public override void Enable() {
        setupHook ??= Common.Hook<AddonCardShopDialogOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 54 41 56 41 57 48 83 EC 50 48 8B F9", SetupDetour);
        setupHook?.Enable();
        base.Enable();
    }

    private void* SetupDetour(AddonShopCardDialog* addonShopCardDialog, uint a2, void* a3) {
        var ret = setupHook.Original(addonShopCardDialog, a2, a3);
        if (addonShopCardDialog == null) return ret;
        if (addonShopCardDialog->CardQuantityInput == null) return ret;
        addonShopCardDialog->CardQuantityInput->SetValue(addonShopCardDialog->CardQuantityInput->Data.Max);
        return ret;
    }

    public override void Disable() {
        setupHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        setupHook?.Dispose();
        base.Dispose();
    }
}