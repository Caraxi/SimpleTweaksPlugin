using System;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Increase max line count in chat bubbles")]
[TweakDescription("Allow upto 7 lines of text to be displayed in chat bubbles.")]
[TweakCategory(TweakCategory.Chat)]
[TweakReleaseVersion(UnreleasedVersion)]
[TweakAutoConfig]
public unsafe class IncreaseChatBubbleLines : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Max Lines", IntMin = 1, IntMax = 7, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int MaxLines = 5;
    }
    [TweakConfig] public Configs TweakConfig { get; private set; } = new();
    
    [StructLayout(LayoutKind.Explicit)]
    private struct ChatBubbleStruct {
        [FieldOffset(0x8C)] public byte LineCount;
    }
    
    private delegate ulong ChatBubble(ChatBubbleStruct* a1);
    
    [TweakHook, Signature("E8 ?? ?? ?? ?? 0F B6 E8 48 8D 5F 18 40 0A 6C 24 ?? BE", DetourName = nameof(ChatBubbleDetour))]
    private HookWrapper<ChatBubble> chatBubbleHook;

    private ulong ChatBubbleDetour(ChatBubbleStruct* a1) {
        try {
            return chatBubbleHook.Original(a1);
        } finally {
            a1->LineCount = (byte) Math.Clamp(TweakConfig.MaxLines, 1, 7);
        }
    }
}
