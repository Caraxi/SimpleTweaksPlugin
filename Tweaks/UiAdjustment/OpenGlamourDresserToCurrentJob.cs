using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Open Glamour Dresser to Current Job")]
[TweakAuthor("MidoriKami")]
[TweakDescription("Sets the job selection dropdown to your current job when opening the Glamour Dresser.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class OpenGlamourDresserToCurrentJob : UiAdjustments.SubTweak {
    [AddonSetup("MiragePrismPrismBox")]
    private void OnMiragePrismBoxOpen(AtkUnitBase* atkUnitBase) {
        if (Service.ClientState is { LocalPlayer.ClassJob.Id: var playerJob }) {
            *((byte*) atkUnitBase + 400) = (byte)playerJob;
        }
    }
}