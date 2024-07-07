using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Disable Chat Movement")]
[TweakDescription("Prevents movement of the chat window.")]
public unsafe class DisableChatMovement : ChatTweaks.SubTweak {
    private delegate void* SetUiPositionDelegate(AtkUnitManager* atkUnitManager, AtkUnitBase* unitBase, ulong a3);

    private delegate void ChatPanelDragControlDelegate(AtkAddonControl* addonControl, ulong controlCode, ulong a3, nint a4, short* a5);

    [TweakHook, Signature("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??", DetourName = nameof(SetUiPositionDetour))]
    private HookWrapper<SetUiPositionDelegate> setUiPositionHook;

    [TweakHook, Signature("40 53 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B F1", DetourName = nameof(ChatPanelControlDetour))]
    private HookWrapper<ChatPanelDragControlDelegate> chatPanelDragControlHook;

    private void ChatPanelControlDetour(AtkAddonControl* addonControl, ulong controlCode, ulong a3, nint a4, short* a5) {
        if (controlCode == 0x17) return; // Suppress Start Drag
        chatPanelDragControlHook.Original(addonControl, controlCode, a3, a4, a5);
    }

    private void* SetUiPositionDetour(AtkUnitManager* atkUnitManager, AtkUnitBase* unitBase, ulong a3) {
        if (unitBase->NameString.StartsWith("ChatLog")) return null;
        return setUiPositionHook.Original(atkUnitManager, unitBase, a3);
    }
}
