using Dalamud.Hooking;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class DisableRightClickTargeting : Tweak {
        public override string Name => "Disable Right Click Targeting";

        private delegate void* RightClickTarget(void** a1, void* a2, bool a3);
        private Hook<RightClickTarget> rightClickTargetHook;
        
        public override void Enable() {
            rightClickTargetHook ??= new Hook<RightClickTarget>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B"), new RightClickTarget(RightClickTargetDetour));
            rightClickTargetHook?.Enable();
            base.Enable();
        }
        
        public override void Disable() {
            rightClickTargetHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            rightClickTargetHook?.Dispose();
            base.Dispose();
        }

        private void* RightClickTargetDetour(void** a1, void* a2, bool a3) {
            if (a2 != null && a2 == a1[16]) return rightClickTargetHook.Original(a1, a2, a3);
            return null;
        }
    }
}
