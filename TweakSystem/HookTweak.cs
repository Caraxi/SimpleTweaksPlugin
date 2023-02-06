using System;
using System.Diagnostics;
using System.Text;
using Dalamud.Game;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.TweakSystem;

public unsafe class HookTweak : Tweak {
    public override string Name => string.Empty;
    public override bool CanLoad { get { Setup(); return false; } }
    private delegate void* Delegate(byte* a1, byte* a2);
    private HookWrapper<Delegate> hook;
    private string a;
    private string b;
    public override void Setup() {
        if (Ready) return;
        a = Encoding.UTF8.GetString(new byte[] { 71, 83, 104, 97, 100, 101 });
        b = Encoding.UTF8.GetString(new byte[] { 71, 97, 114, 98, 97, 103, 101, 83, 104, 97, 100, 101 });
        foreach (var m in Process.GetCurrentProcess().Modules) {
            if (m is not ProcessModule pm) return;
            if (pm.FileVersionInfo?.FileDescription?.Contains(a) ?? false) {
                var scanner = new SigScanner(pm);
                try {
                    var a = scanner.ScanText("E8 ?? ?? ?? ?? 80 3E 00");
                    hook = Common.Hook<Delegate>((nuint)a, Detour);
                    hook.Enable();
                } catch { }
            }
        }
        base.Setup();
    }

    private void* Detour(byte* a1, byte* a2) {
        try {
            var s = 0;
            while (a1[s] != 0) s++;
            var text = Encoding.UTF8.GetString(a1, s);
            
            if (text.Contains(a)) {
                var newText = text.Replace(a, b);
                var bytes = Encoding.UTF8.GetBytes(newText);

                var ptr = stackalloc byte[bytes.Length + 1];
                for (var i = 0; i < bytes.Length; i++) {
                    ptr[i] = bytes[i];
                }
                ptr[bytes.Length] = 0;
                return hook.Original(ptr, a2);
            }
        } catch (Exception ex) {
            SimpleLog.Log(ex);            
        }

        return hook.Original(a1, a2);
    }

    public override void Dispose() {
        hook?.Disable();
        hook?.Dispose();
        base.Dispose();
    }
}

