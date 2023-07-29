using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class ExtendedMacroIcon : Tweak {
    public override string Name => "Extended Macro Icons";
    public override string Description => "Allow using specific Icon IDs when using '/macroicon # id' inside of a macro.";

    private delegate ulong SetupMacroIconDelegate(RaptureMacroModule* macroModule, UIModule* uiModule, byte* outCategory, int* outId, uint macroPage, uint macroIndex, void* a7);
    private HookWrapper<SetupMacroIconDelegate> setupMacroIconHook;

    private delegate ulong GetIconIdDelegate(void* a1, ulong a2, ulong a3);
    private HookWrapper<GetIconIdDelegate> getIconIdHook;

    public override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        base.Setup();
    }

    private const byte IconCategory = 0xFF;
    
    [StructLayout(LayoutKind.Explicit, Size = 0x120)]
    public struct MacroIconTextCommand {
        [FieldOffset(0x00)] public ushort TextCommandId;
        [FieldOffset(0x08)] public int Id;
        [FieldOffset(0x0C)] public int Category;
    }

    protected override void Enable() {
        setupMacroIconHook ??= Common.Hook<SetupMacroIconDelegate>("E8 ?? ?? ?? ?? 0F B6 BE ?? ?? ?? ?? 48 8B CD", SetupMacroIconDetour);
        setupMacroIconHook?.Enable();

        getIconIdHook ??= Common.Hook<GetIconIdDelegate>("E8 ?? ?? ?? ?? 85 C0 89 83 ?? ?? ?? ?? 0F 94 C0", GetIconIdDetour);
        getIconIdHook?.Enable();
        base.Enable();
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

    protected override void Disable() {
        setupMacroIconHook?.Disable();
        getIconIdHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        setupMacroIconHook?.Dispose();
        getIconIdHook?.Dispose();
        base.Dispose();
    }
}