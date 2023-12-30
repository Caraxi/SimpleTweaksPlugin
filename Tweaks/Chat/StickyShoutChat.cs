using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat; 

[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class StickyShoutChat : ChatTweaks.SubTweak {
    public override string Name => "Sticky Shout Chat";
    public override string Description => "Prevents the game from automatically switching out of shout chat.";
    
    private delegate void FocusPreviousChatChannelDelegate(RaptureShellModule* self);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 33 C0 49 8B CE", DetourName = nameof(SkipFocusChannel))]
    private readonly HookWrapper<FocusPreviousChatChannelDelegate> focusPreviousChannelHook;

    private void SkipFocusChannel(RaptureShellModule* self) { }
}