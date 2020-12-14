using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin.Tweaks {
    internal class DisableTitleScreenMovie : Tweak {

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        private IntPtr injectAddress;

        public override string Name => "Disable Title Screen Movie";
        public override void Setup() {
            try {
                injectAddress = PluginInterface.TargetModuleScanner.ScanText("60 EA 00 00 0F 86");
                Ready = true;
            } catch {
                SimpleLog.Error($"Failed to find address for '{Name}'");
            }
        }

        public override void Enable() {
            if (!Ready) return;
            if (Enabled) return;
            WriteProcessMemory(Process.GetCurrentProcess().Handle, injectAddress, new byte[] {0x99, 0x99, 0x99, 0x99}, 4, out _);
            Enabled = true;
        }

        public override void Disable() {
            if (!Ready) return;
            if (!Enabled) return;
            WriteProcessMemory(Process.GetCurrentProcess().Handle, injectAddress, new byte[] { 0x60, 0xEA, 0x00, 0x00 }, 4, out _);
            Enabled = false;
        }

        public override void Dispose() {
            if (Enabled) Disable();
            Enabled = false;
            Ready = false;
        }
    }
}
