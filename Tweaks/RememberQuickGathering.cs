using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Remember Quick Gathering")]
[TweakDescription("Remembers quick gathering status even after gathering at unspoiled nodes.")]
public unsafe class RememberQuickGathering : Tweak {
    private delegate void SetupGatheringPointDelegate(GatheringPointEventHandler* handler);

    [TweakHook, Signature("E8 ?? ?? ?? ?? EB 0A 45 33 C0 33 D2 E8 ?? ?? ?? ?? 45 8B CE", DetourName = nameof(SetupGatheringPoint))]
    private HookWrapper<SetupGatheringPointDelegate> setupGatheringPointHook;

    private void SetupGatheringPoint(GatheringPointEventHandler* gatheringPointEventHandler) {
        setupGatheringPointHook.Original(gatheringPointEventHandler);
        gatheringPointEventHandler->QuickGatheringEnabled = RaptureAtkModule.Instance()->QuickGatheringEnabled;
    }
}
