using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Disable Novice Network Auto-Switch")]
[TweakDescription("Disables automatically selecting novice network when logging in or transferring to another server.")]
[TweakReleaseVersion("1.10.6.0")]
public unsafe class DisableNoviceNetworkAutoSwitch : Tweak {
    private delegate void JoinNoviceNetworkDelegate(AgentChatLog* agentChatLog, uint numMentor, uint numNewAdventurer);

    [TweakHook, Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 89 53 20", DetourName = nameof(JoinNoviceNetworkDetour))]
    private HookWrapper<JoinNoviceNetworkDelegate> joinNoviceNetworkHook = null!;

    [TweakHook(typeof(RaptureShellModule), nameof(RaptureShellModule.ChangeChatChannel), nameof(ChangeChatChannelDetour), AutoEnable = false)]
    private HookWrapper<RaptureShellModule.Delegates.ChangeChatChannel> changeChatChannelHook = null!;

    private void JoinNoviceNetworkDetour(AgentChatLog* agentChatLog, uint numMentor, uint numNewAdventurer) {
        changeChatChannelHook.Enable();
        joinNoviceNetworkHook.Original(agentChatLog, numMentor, numNewAdventurer);
    }

    private void ChangeChatChannelDetour(RaptureShellModule* shellModule, int channel, uint linkshellIndex, Utf8String* target, bool setChatType) {
        changeChatChannelHook.Disable();
        if (channel == 8) return;
        SimpleLog.Warning($"Tweak attempted to cancel ChangeChatChannel for incorrect channel#{channel}.");
        changeChatChannelHook.Original(shellModule, channel, linkshellIndex, target, setChatType);
    }
}
