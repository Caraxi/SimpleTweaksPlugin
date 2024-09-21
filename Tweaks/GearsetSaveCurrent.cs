using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Gearset Update Command")]
[TweakDescription("Updates the active gearset with your current equipment.")]
[TweakReleaseVersion("1.9.4.0")]
public unsafe class GearsetSaveCurrent : CommandTweak {
    protected override string Command => "/updategearset";

    protected override void OnCommand(string args) {
        var module = RaptureGearsetModule.Instance();
        if (module->CurrentGearsetIndex < 0 || !module->IsValidGearset(module->CurrentGearsetIndex)) {
            Service.Chat.PrintError("No gearset is active.");
            return;
        }

        var activeGearset = module->GetGearset(module->CurrentGearsetIndex);
        if (activeGearset == null) return;

        if (activeGearset->ClassJob != UIState.Instance()->PlayerState.CurrentClassJobId) {
            Service.Chat.PrintError("Active gearset class does not match current class.");
            return;
        }

        module->UpdateGearset(module->CurrentGearsetIndex);
    }
}
