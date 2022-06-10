using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class RecommendEquipCommand : CommandTweak {
    public override string Name => "Equip Recommended Command";
    public override string Description => $"Adds /{Command} to equip recommended gear.";
    protected override string HelpMessage => "Equips recommended gear.";
    protected override string Command => "equiprecommended";
    private static RecommendEquipModule* Module => Framework.Instance()->GetUiModule()->GetRecommendEquipModule();
    
    protected override void OnCommand(string args) {
        if (Module == null) return;
        Module->SetupRecommendedGear();
        Common.FrameworkUpdate += DoEquip;
    }

    private static void DoEquip() {
        if (Module == null || Module->EquippedMainHand == null) {
            Common.FrameworkUpdate -= DoEquip;
            return;
        }
        Module->EquipRecommendedGear();
    }

    public override void Disable() {
        Common.FrameworkUpdate -= DoEquip;
        base.Disable();
    }
}
