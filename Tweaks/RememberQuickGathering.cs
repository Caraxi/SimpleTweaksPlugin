using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Remember Quick Gathering")]
[TweakDescription("Remembers quick gathering status even after gathering at unspoiled nodes.")]
public unsafe class RememberQuickGathering : Tweak {
    #region TEMP Structs

    // TODO: Remove after CS Update
    [StructLayout(LayoutKind.Explicit, Size = 0x29478)]
    private struct RaptureAtkModule {
        [FieldOffset(0x2939D)] public byte QuickGatheringEnabled;

        public static RaptureAtkModule* Instance() => (RaptureAtkModule*)FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule.Instance();
    }

    // TODO: Remove after CS Update
    [StructLayout(LayoutKind.Explicit, Size = 0x470)]
    private struct GatheringPointEventHandler {
        [FieldOffset(0x46D)] public byte QuickGatheringEnabled;
    }

    #endregion

    private delegate void SetupGatheringPointDelegate(GatheringPointEventHandler* handler);

    [TweakHook, Signature("E8 ?? ?? ?? ?? EB 0A 45 33 C0 33 D2 E8 ?? ?? ?? ?? 45 8B CE", DetourName = nameof(SetupGatheringPoint))]
    private HookWrapper<SetupGatheringPointDelegate> setupGatheringPointHook;

    private void SetupGatheringPoint(GatheringPointEventHandler* gatheringPointEventHandler) {
        setupGatheringPointHook.Original(gatheringPointEventHandler);
        gatheringPointEventHandler->QuickGatheringEnabled = RaptureAtkModule.Instance()->QuickGatheringEnabled;
    }
}
