using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Increase max line count in chat bubbles")]
[TweakDescription("Allow upto 7 lines of text to be displayed in chat bubbles.")]
[TweakCategory(TweakCategory.Chat)]
[TweakReleaseVersion("1.10.11.1")]
[Changelog("1.10.12.1", "Added ability to adjust the duration of chat bubbles.")]
[TweakAutoConfig]
public unsafe class IncreaseChatBubbleLines : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Max Lines", IntMin = 1, IntMax = 7, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int MaxLines = 5;
        public int Duration = 4000;
        public int AddDurationPerCharacter = 0;

    }
    [TweakConfig] public Configs TweakConfig { get; private set; } = new();
    
    protected void DrawConfig(ref bool hasChanged) {
        var timeSeconds = TweakConfig.Duration / 1000f;
        ImGui.SetNextItemWidth(110 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("Bubble Duration (seconds)", ref timeSeconds, 0.1f, 1f, "%.1f")) {
            TweakConfig.Duration = Math.Clamp((int)MathF.Round(timeSeconds * 10), 10, 600) * 100;
            hasChanged = true;
        }

        ImGui.SetNextItemWidth(110 * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.InputInt("Additional time per character of message (milliseconds)", ref TweakConfig.AddDurationPerCharacter, 1, 10);
    }
    
    
    [StructLayout(LayoutKind.Explicit)]
    private struct ChatBubbleStruct {
        [FieldOffset(0x8C)] public byte LineCount;
    }
    
    private delegate ulong ChatBubble(ChatBubbleStruct* a1);
    
    [TweakHook, Signature("E8 ?? ?? ?? ?? 0F B6 E8 48 8D 5F 18 40 0A 6C 24 ?? BE", DetourName = nameof(ChatBubbleDetour))]
    private HookWrapper<ChatBubble> chatBubbleHook;

    [StructLayout(LayoutKind.Explicit)]
    public struct ChatBubbleEntry {
        [FieldOffset(0x000)] public Utf8String String;
        [FieldOffset(0x1B8)] public long Timestamp;
    }
    
    private delegate uint GetStringSize(TextChecker* textChecker, Utf8String* str);
    [Signature("E8 ?? ?? ?? ?? 49 8D 56 40")]
    private GetStringSize getStringSize;
    
    private ulong ChatBubbleDetour(ChatBubbleStruct* a1) {
        try {
            return chatBubbleHook.Original(a1);
        } finally {
            try {
                a1->LineCount = (byte)Math.Clamp(TweakConfig.MaxLines, 1, 7);

                newBubbles.RemoveWhere(b => {
                    var bubble = (ChatBubbleEntry*)b;
                    if (bubble->Timestamp < 200) {
                        if (bubble->Timestamp >= 0) bubble->Timestamp++; // Safety to throw bubbles away after a few seconds.
                        // Wait for bubble to be displayed.
                        return false;
                    }

                    // Future Bubbles
                    bubble->Timestamp += (TweakConfig.Duration - 4000);
                    if (TweakConfig.AddDurationPerCharacter > 0) {
                        var characters = getStringSize(&RaptureTextModule.Instance()->TextChecker, &bubble->String);
                        var additionalDuration = TweakConfig.AddDurationPerCharacter * Math.Clamp(characters, 0, 194 * TweakConfig.MaxLines);
                        bubble->Timestamp += additionalDuration;
                    }

                    return true;
                });
            } catch(Exception ex) {
                SimpleLog.Error(ex, "Error handing chat bubbles.");
            }
        }
    }
    
    private delegate byte SetupChatBubble(nint unk, nint a2, nint a3);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 49 FF 46 60", DetourName = nameof(SetupChatBubbleDetour))]
    private HookWrapper<SetupChatBubble> setupChatBubbleHook;
    
    private readonly HashSet<nint> newBubbles = new();
        
    private byte SetupChatBubbleDetour(nint unk, nint a2, nint a3) {
        try {
            var ret = setupChatBubbleHook.Original(unk, a2, a3);
            if (TweakConfig.Duration != 4000 || TweakConfig.AddDurationPerCharacter > 0) newBubbles.Add(a2);
            return ret;
        } catch {
            return 0;
        }
    }
}
