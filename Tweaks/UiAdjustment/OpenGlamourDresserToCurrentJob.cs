using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Open Glamour Dresser to Current Job")]
[TweakAuthor("MidoriKami")]
[TweakDescription("Sets the job selection dropdown to your current job when opening the Glamour Dresser.")]
[TweakReleaseVersion("1.9.0.0")]
public unsafe class OpenGlamourDresserToCurrentJob : UiAdjustments.SubTweak {
    [AddonPreSetup("MiragePrismPrismBox")]
    private void OnMiragePrismBoxOpen(AtkUnitBase* atkUnitBase) {
        if (Service.ClientState is { LocalPlayer.ClassJob.Id: var playerJob }) {
            Marshal.WriteByte((nint)atkUnitBase, 416, (byte)playerJob);
        }
    }
}