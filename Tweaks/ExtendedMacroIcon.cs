using System;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Extended Macro Icons")]
[TweakDescription("Allow using specific Icon IDs when using '/macroicon # id' inside of a macro.")]
[TweakReleaseVersion("1.8.3.0")]
public unsafe class ExtendedMacroIcon : Tweak {
    private delegate ulong SetupMacroIconDelegate(RaptureMacroModule* macroModule, UIModule* uiModule, byte* outCategory, int* outId, uint macroPage, uint macroIndex, void* a7);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 0F B6 BE ?? ?? ?? ?? 8B 9E", DetourName = nameof(SetupMacroIconDetour))]
    private HookWrapper<SetupMacroIconDelegate> setupMacroIconHook;

    private delegate ulong GetIconIdDelegate(void* a1, ulong a2, ulong a3);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 85 C0 89 83 ?? ?? ?? ?? 0F 94 C0", DetourName = nameof(GetIconIdDetour))]
    private HookWrapper<GetIconIdDelegate> getIconIdHook;

    private const byte IconCategory = 0xFF;

    [StructLayout(LayoutKind.Explicit, Size = 0x120)]
    public struct MacroIconTextCommand {
        [FieldOffset(0x00)] public ushort TextCommandId;
        [FieldOffset(0x08)] public int Id;
        [FieldOffset(0x0C)] public int Category;
    }

    private ulong SetupMacroIconDetour(RaptureMacroModule* macroModule, UIModule* uiModule, byte* outCategory, int* outId, uint macroPage, uint macroIndex, void* a7) {
        try {
            var macro = macroModule->GetMacro(macroPage, macroIndex);
            var shellModule = uiModule->GetRaptureShellModule();
            var result = stackalloc MacroIconTextCommand[1];

            if (shellModule->TryGetMacroIconCommand(macro, result) && result->TextCommandId == 207 && result->Category is 270 or 271 && result->Id > 0) {
                *outCategory = IconCategory;
                *outId = result->Id;
                return 1;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return setupMacroIconHook.Original(macroModule, uiModule, outCategory, outId, macroPage, macroIndex, a7);
    }

    private ulong GetIconIdDetour(void* a1, ulong category, ulong id) => category == IconCategory ? id : getIconIdHook.Original(a1, category, id);
}
