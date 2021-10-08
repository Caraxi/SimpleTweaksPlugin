using System;
using Dalamud.Hooking;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    
    public class DisableChatMovement : ChatTweaks.SubTweak {

        public override string Name => "Disable Chat Movement";
        public override string Description => "Prevents movement of the chat window.";

        private delegate IntPtr SetUiPositionDelegate(IntPtr _this, IntPtr uiObject, ulong y);

        private unsafe delegate void ChatPanelDragControlDelegate(IntPtr _this, ulong controlCode, ulong a3, IntPtr a4, short* a5);

        private IntPtr setUiPositionAddress = IntPtr.Zero;
        private IntPtr chatPanelControlAddress = IntPtr.Zero;

        private Hook<SetUiPositionDelegate> setUiPositionHook;
        private Hook<ChatPanelDragControlDelegate> chatPanelDragControlHook;
        
        public override void Setup() {
            if (Ready) return;

            try {
                if (setUiPositionAddress == IntPtr.Zero) {
                    setUiPositionAddress = Service.SigScanner.ScanText("40 53 48 83 EC 20 80 A2 ?? ?? ?? ?? ??");
                }

                if (chatPanelControlAddress == IntPtr.Zero) {
                    chatPanelControlAddress = Service.SigScanner.ScanText("40 55 57 48 81 EC ?? ?? ?? ?? 48 8B F9 45 8B C8");
                }
                
                if (setUiPositionAddress == IntPtr.Zero || chatPanelControlAddress == IntPtr.Zero) {
                    SimpleLog.Error($"Failed to setup {GetType().Name}: Failed to find required functions.");
                    return;
                }

                Ready = true;

            } catch (Exception ex) {
                SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
            }
        }

        public override unsafe void Enable() {
            if (!Ready) return;
            setUiPositionHook ??= new Hook<SetUiPositionDelegate>(setUiPositionAddress, new SetUiPositionDelegate(SetUiPositionDetour));
            chatPanelDragControlHook ??= new Hook<ChatPanelDragControlDelegate>(chatPanelControlAddress, new ChatPanelDragControlDelegate(ChatPanelControlDetour));

            setUiPositionHook?.Enable();
            chatPanelDragControlHook?.Enable();
            Enabled = true;
        }

        private unsafe void ChatPanelControlDetour(IntPtr a1, ulong controlCode, ulong a3, IntPtr a4, short* a5) {
            if (controlCode == 0x17) return; // Suppress Start Drag
            chatPanelDragControlHook.Original(a1, controlCode, a3, a4, a5);
        }

        private unsafe IntPtr SetUiPositionDetour(IntPtr _this, IntPtr uiObject, ulong a3) {
            var k = *(ulong*) (uiObject + 8);
            if (k == 0x50676F4C74616843 || k == 0x676F4C74616843) {
                // Suppress Movement of "ChatLog" and "ChatLogPanel_*"
                return IntPtr.Zero;
            }

            return setUiPositionHook.Original(_this, uiObject, a3);
        }

        public override void Disable() {
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
}
