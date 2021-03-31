using System;
using Dalamud;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class LegacyCameraLock : Tweak {
        public override string Name => "Legacy Camera Lock";
        public override string Description => "Prevents camera rotation when using Legacy movement type.";

        private IntPtr changeAddress = IntPtr.Zero;
        private byte[] originalBytes = new byte[6];
        
        public override void Enable() {
            if (Enabled) return;
            try {
                changeAddress = Common.Scanner.ScanText("0F 86 ?? ?? ?? ?? 48 8B 4D 77");
                SimpleLog.Verbose($"Found Signature: {changeAddress.ToInt64():X}");
            } catch {
                SimpleLog.Error("Failed to find Signature");
                return;
            }
            if (changeAddress == IntPtr.Zero) return;
            
            originalBytes = new byte[6];
            var readOriginalSuccess = SafeMemory.ReadBytes(changeAddress, 6, out originalBytes);
            if (readOriginalSuccess) {
                var relAddr = BitConverter.ToInt32(originalBytes, 2) + 1;
                var relAddrBytes = BitConverter.GetBytes(relAddr);
                var writeNewSuccess = SafeMemory.WriteBytes(changeAddress, new byte[6] {0xE9, relAddrBytes[0], relAddrBytes[1], relAddrBytes[2], relAddrBytes[3], 0x90});
                if (writeNewSuccess) {
                    base.Enable();
                } else {
                    SimpleLog.Error("Failed to write new instruction");
                }
            } else {
                originalBytes = new byte[0];
                SimpleLog.Error("Failed to read original instruction");
            }
        }

        public override void Disable() {
            if (!Enabled) return;
            if (changeAddress == IntPtr.Zero) return;
            
            var writeOriginalSuccess = SafeMemory.WriteBytes(changeAddress, originalBytes);
            if (!writeOriginalSuccess) {
                SimpleLog.Error("Failed to write original instruction");
            }
            base.Disable();
        }

        public override void Dispose() {
            if (Enabled) Disable();
        }
    }
}

