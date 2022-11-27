using System;
using System.Runtime.InteropServices;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class EmoteLogSubcommand : Tweak {
    public override string Name => "Emote Log Subcommand";
    public override string Description => "Adds a 'text' subcommand for emotes when emotelog is disabled.  /yes text";

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct EmoteCommandStruct {
        [FieldOffset(0x08)] public short TextCommandParam;
    }

    private delegate void* ExecuteEmoteCommand(void* a1, EmoteCommandStruct* command, void* a3);
    private HookWrapper<ExecuteEmoteCommand> executeEmoteCommandHook;
    
    public override void Enable() {
        executeEmoteCommandHook ??= Common.Hook<ExecuteEmoteCommand>("4C 8B DC 53 55 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 2D", ExecuteDetour);
        executeEmoteCommandHook?.Enable();
        base.Enable();
    }
    
    private bool EmoteTextType {
        get => GameConfig.UiConfig.GetBool("EmoteTextType");
        set => GameConfig.UiConfig.Set("EmoteTextType", value);
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
        }finally {
            if (didEnable) EmoteTextType = false;
        }
    }

    public override void Disable() {
        executeEmoteCommandHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        executeEmoteCommandHook?.Dispose();
        base.Dispose();
    }
}
