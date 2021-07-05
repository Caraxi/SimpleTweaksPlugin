using System;
using Dalamud;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class EnhancedAutoFaceTarget : Tweak {
        public override string Name => "Enhanced Auto Face Target";
        public override string Description => "Changes the auto face target setting to only apply when necessary.";
        protected override string Author => "UnknownX";

        private IntPtr changeAddress = IntPtr.Zero;
        private byte[] originalBytes = new byte[5];

        public override void Enable() {
            if (Enabled) return;
            changeAddress = Common.Scanner.ScanText("41 80 7E 2F 06 75 1E 48 8D 0D");
            if (SafeMemory.ReadBytes(changeAddress, 5, out originalBytes)) {
                if (SafeMemory.WriteBytes(changeAddress, new byte[] {0x41, 0xF6, 0x46, 0x36, 0x10})) { // cmp byte ptr [r14+2Fh], 6 -> test byte ptr [r14+36h], 10
                    base.Enable();
                } else {
                    SimpleLog.Error("Failed to write new instruction");
                }
            } else {
                SimpleLog.Error("Failed to read original instruction");
            }
        }

        public override void Disable() {
            if (!Enabled) return;
            if (!SafeMemory.WriteBytes(changeAddress, originalBytes)) {
                SimpleLog.Error("Failed to write original instruction");
            }
            base.Disable();
        }

        public override void Dispose() {
            Disable();
            base.Dispose();
        }
    }
}

