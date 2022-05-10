using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class SellMaxTripleTriadCards : UiAdjustments.SubTweak {
    public override string Name => "Default to max when selling Triple Triad Cards";
    public override string Description => "Set the default number of cards to sell at the Triple Triad Trader to the number of cards you have.";
    
    public override void Enable() {
        Common.AddonSetup += SetupDetour;
        base.Enable();
    }

    private void SetupDetour(SetupAddonArgs args) {
        if (args.AddonName != "ShopCardDialog") return;
        var addonShopCardDialog = (AddonShopCardDialog*)args.Addon;
        if (addonShopCardDialog == null) return;
        if (addonShopCardDialog->CardQuantityInput == null) return;
        addonShopCardDialog->CardQuantityInput->SetValue(addonShopCardDialog->CardQuantityInput->Data.Max);
        return;
    }

    public override void Disable() {
        Common.AddonSetup -= SetupDetour;
        base.Disable();
    }
}