using System;
using Dalamud.Hooking;

namespace SimpleTweaksPlugin.Tweaks.Chat {
    public class DisableChatResize : ChatTweaks.SubTweak {
        public override string Name => "Disable Chat Resize";
        public override string Description => "Prevents resizing of the chat window.";

        private unsafe delegate void MainChatWindowControlDelegate(IntPtr uiObject, ushort controlCode, IntPtr a3, IntPtr* a4, IntPtr a5);
        private unsafe delegate void SubChatWindowControlDelegate(IntPtr a1, ushort controlCode, uint a3, IntPtr a4, ushort* a5);

        private IntPtr mainChatWindowControlAddress = IntPtr.Zero;
        private IntPtr subChatWindowControlAddress = IntPtr.Zero;

        private Hook<MainChatWindowControlDelegate> mainChatWindowControlHook;
        private Hook<SubChatWindowControlDelegate> subChatWindowControlHook;

        private const ushort ResizeControlCode = 0x17;

        public override void Setup() {
            if (Ready) return;

            try {

                if (mainChatWindowControlAddress == IntPtr.Zero) {
                    mainChatWindowControlAddress = Service.SigScanner.ScanText("40 55 41 54 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 1F 80 B9 ?? ?? ?? ?? ??");
                }

                if (subChatWindowControlAddress == IntPtr.Zero) {
                    subChatWindowControlAddress = Service.SigScanner.ScanText("40 55 53 56 41 54 41 56 41 57 48 8D AC 24");
                }

                if (mainChatWindowControlAddress == IntPtr.Zero || subChatWindowControlAddress == IntPtr.Zero) {
                    SimpleLog.Error($"Failed to setup {GetType().Name}: Failed to find required functions.");
                    return;
                }

                Ready = true;

            } catch (Exception ex) {
                SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
            }
        }

        private unsafe void SubChatWindowControlDetour(IntPtr a1, ushort controlCode, uint a3, IntPtr a4, ushort* a5) {
            if (controlCode == ResizeControlCode) return;
            subChatWindowControlHook?.Original(a1, controlCode, a3, a4, a5);
        }

        private unsafe void MainChatWindowControlDetour(IntPtr uiObject, ushort controlCode, IntPtr a3, IntPtr* a4, IntPtr a5) {
            if (controlCode == ResizeControlCode) return;
            mainChatWindowControlHook?.Original(uiObject, controlCode, a3, a4, a5);
        }

        public override unsafe void Enable() {
            if (!Ready) return;
            mainChatWindowControlHook ??= new Hook<MainChatWindowControlDelegate>(mainChatWindowControlAddress, new MainChatWindowControlDelegate(MainChatWindowControlDetour));
            subChatWindowControlHook ??= new Hook<SubChatWindowControlDelegate>(subChatWindowControlAddress, new SubChatWindowControlDelegate(SubChatWindowControlDetour));

            mainChatWindowControlHook?.Enable();
            subChatWindowControlHook?.Enable();
            Enabled = true;
        }

        public override void Disable() {
            mainChatWindowControlHook?.Disable();
            subChatWindowControlHook?.Disable();
            Enabled = false;
        }

        public override void Dispose() {
            mainChatWindowControlHook?.Dispose();
            subChatWindowControlHook?.Dispose();
            Enabled = false;
            Ready = false;
        }
    }
}
