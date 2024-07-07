using System;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakCategory(TweakCategory.Command)]
[TweakName("Emote Log Subcommand")]
[TweakDescription("Adds a 'text' subcommand for emotes when emotelog is disabled.  /yes text")]
public unsafe class EmoteLogSubcommand : Tweak {
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct EmoteCommandStruct {
        [FieldOffset(0x08)] public short TextCommandParam;
    }

    private delegate void* ExecuteEmoteCommand(void* a1, EmoteCommandStruct* command, void* a3);

    [TweakHook, Signature("4C 8B DC 53 55 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 2D", DetourName = nameof(ExecuteDetour))]
    private HookWrapper<ExecuteEmoteCommand> executeEmoteCommandHook;

    private bool EmoteTextType {
        get => Service.GameConfig.UiConfig.GetBool("EmoteTextType");
        set => Service.GameConfig.UiConfig.Set("EmoteTextType", value);
    }

    private void* ExecuteDetour(void* a1, EmoteCommandStruct* command, void* a3) {
        var didEnable = false;
        try {
            if (command->TextCommandParam is 20 or 21) {
                if (!EmoteTextType) {
                    EmoteTextType = didEnable = true;
                }
            }

            return executeEmoteCommandHook.Original(a1, command, a3);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            return executeEmoteCommandHook.Original(a1, command, a3);
        } finally {
            if (didEnable) EmoteTextType = false;
        }
    }
}
