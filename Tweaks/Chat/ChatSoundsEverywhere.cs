#nullable enable
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Chat Sounds Everywhere")]
[TweakDescription("Enables <se.#> chat sounds everywhere, regardless of channel.")]
[TweakAuthor("Asriel")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class ChatSoundsEverywhere : ChatTweaks.SubTweak {
    private delegate Utf8String* PronounModuleProcessChatStringDelegate(PronounModule* a1, Utf8String* a2, bool a3);
    [TweakHook, Signature("E8 ?? ?? ?? ?? 0F B7 7F 08", DetourName = nameof(PronounModuleProcessChatStringDetour))]
    private readonly HookWrapper<PronounModuleProcessChatStringDelegate> pronounModuleProcessChatString;

    private Utf8String* PronounModuleProcessChatStringDetour(PronounModule* a1, Utf8String* a2, bool _) =>
        pronounModuleProcessChatString.Original(a1, a2, true);
}