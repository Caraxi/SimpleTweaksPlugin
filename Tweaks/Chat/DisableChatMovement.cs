using System;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public class DisableChatMovement : ChatTweaks.SubTweak {

    public override string Name => "Disable Chat Movement";
    public override string Description => "Prevents movement of the chat window.";

    private delegate nint SetUiPositionDelegate(nint _this, nint uiObject, ulong y);

    private unsafe delegate void ChatPanelDragControlDelegate(nint _this, ulong controlCode, ulong a3, nint a4, short* a5);

    private nint setUiPositionAddress = nint.Zero;
    private nint chatPanelControlAddress = nint.Zero;

    private HookWrapper<SetUiPositionDelegate> setUiPositionHook;
    private HookWrapper<ChatPanelDragControlDelegate> chatPanelDragControlHook;
        
    public override void Setup() {
        if (Ready) return;

        try {
            if (setUiPositionAddress == nint.Zero) {
                setUiPositionAddress = Service.SigScanner.ScanText("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??");
            }

            if (chatPanelControlAddress == nint.Zero) {
                chatPanelControlAddress = Service.SigScanner.ScanText("40 55 57 48 81 EC ?? ?? ?? ?? 48 8B F9 45 8B C8");
            }
                
            if (setUiPositionAddress == nint.Zero || chatPanelControlAddress == nint.Zero) {
                SimpleLog.Error($"Failed to setup {GetType().Name}: Failed to find required functions.");
                return;
            }

            Ready = true;

        } catch (Exception ex) {
            SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
        }
    }

    protected override unsafe void Enable() {
        if (!Ready) return;
        setUiPositionHook ??= Common.Hook(setUiPositionAddress, new SetUiPositionDelegate(SetUiPositionDetour));
        chatPanelDragControlHook ??= Common.Hook(chatPanelControlAddress, new ChatPanelDragControlDelegate(ChatPanelControlDetour));

        setUiPositionHook?.Enable();
        chatPanelDragControlHook?.Enable();
        Enabled = true;
    }

    private unsafe void ChatPanelControlDetour(nint a1, ulong controlCode, ulong a3, nint a4, short* a5) {
        if (controlCode == 0x17) return; // Suppress Start Drag
        chatPanelDragControlHook.Original(a1, controlCode, a3, a4, a5);
    }

    private unsafe nint SetUiPositionDetour(nint _this, nint uiObject, ulong a3) {
        var k = *(ulong*) (uiObject + 8);
        if (k == 0x50676F4C74616843 || k == 0x676F4C74616843) {
            // Suppress Movement of "ChatLog" and "ChatLogPanel_*"
            return nint.Zero;
        }

        return setUiPositionHook.Original(_this, uiObject, a3);
    }

    protected override void Disable() {
        setUiPositionHook?.Disable();
        chatPanelDragControlHook?.Disable();
        Enabled = false;
    }

    public override void Dispose() {
        setUiPositionHook?.Dispose();
        chatPanelDragControlHook?.Dispose();
        Enabled = false;
        Ready = false;
    }
}