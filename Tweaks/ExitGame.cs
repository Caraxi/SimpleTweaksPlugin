using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Alt + F4 Exit Game")]
[TweakDescription("Pressing Alt + F4 will cause the game to close safely.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.9.2.0")]
public unsafe class ExitGame : Tweak {
    [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
    private const int WM_CLOSE = 0x10;
    
    [FrameworkUpdate]
    private void FrameworkUpdate() {
        if (UIInputData.Instance()->IsKeyDown(SeVirtualKey.MENU) && UIInputData.Instance()->IsKeyPressed(SeVirtualKey.F4)) {
            SendMessage(Process.GetCurrentProcess().MainWindowHandle, WM_CLOSE, 0, 0);
        }
    }
}