using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;

// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
#pragma warning disable CS0169
#pragma warning disable CS0649

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Keyboard Gaming Mode")]
[TweakAuthor("KazWolfe")]
[TweakDescription("Block Alt-Tab and other keys to keep you in the game.")]
[TweakAutoConfig]
[Changelog("1.10.11.0", "Added option to block Caps Lock key", Author = "KazWolfe")]
public partial class KeyInterrupt : Tweak {
    public class Configs : TweakConfig {
        public bool InCombatOnly = true;

        public bool BlockAltTab = true;
        public bool BlockWinKey = true;
        public bool BlockCtrlEsc = true;
        public bool BlockCapsLock = false;
    }

    private const int WH_KEYBOARD_LL = 13;

    private const uint WM_KEYUP = 0x101;
    private const uint WM_KEYDOWN = 0x100;

    [Flags]
    private enum KeyInfoFlags {
        LLKHF_EXTENDED = 0x01,
        LLKHF_INJECTED = 0x10,
        LLKHF_ALTDOWN = 0x20,
        LLKHF_UP = 0x80,
    }

    private struct KeyInfoStruct {
        public int vkCode;
        private int scanCode;
        public KeyInfoFlags flags;
        private int time;
        private int dwExtraInfo;
    }

    private delegate nint HookHandlerDelegate(int nCode, nint wParam, ref KeyInfoStruct lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowsHookExW(int idHook, HookHandlerDelegate lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, ref KeyInfoStruct lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint hWndParent, nint hWndChildAfter, string lpszClass,
        string? lpszWindow);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial int GetWindowThreadProcessId(nint hWnd, out int processId);

    [LibraryImport("user32.dll")]
    private static partial int GetMessageW(out nint lpMsg, nint hWnd, uint wMsgFilterMin = 0, uint wMsgFilterMax = 0);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint TranslateMessage(nint msg);

    [LibraryImport("user32.dll")]
    private static partial nint DispatchMessageW(nint msg);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    public Configs Config { get; private set; }

    private nint _keyboardHookId;
    private Thread? _thread;

    // Storing the delegate as a class member prevents it from being GC'd and causing crashes
    // See: https://stackoverflow.com/a/65250050
    private HookHandlerDelegate? _delegate;

    private CancellationTokenSource? _cts;

    protected override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        base.Setup();
    }

    protected override void Enable() {
        this._delegate = this.OnKeystroke;
        this._cts = new CancellationTokenSource();

        this._thread = new Thread(() => {
            using var currentModule = Process.GetCurrentProcess().MainModule!;

            // SetWindowsHookEx will bind to the currently-used thread.
            // We offload this so that we don't hang the entire system on waiting for framework ticks and all.
            this._keyboardHookId = SetWindowsHookExW(
                WH_KEYBOARD_LL,
                this._delegate,
                GetModuleHandleW(currentModule.ModuleName),
                0);

            while (!this._cts.IsCancellationRequested) {
                // FIXME: This isn't great because we can hang the thread here for a *long* time in theory.
                // In practice, this won't actually happen because our message bus is almost constantly getting data,
                // but this is a slight code smell I can't be bothered to resolve.
                if (GetMessageW(out var msg, 0) != 0) break;

                TranslateMessage(msg);
                DispatchMessageW(msg);
            }
        });
        this._thread.Start();

        base.Enable();
    }

    protected override void Disable() {
        if (this._keyboardHookId != nint.Zero) {
            UnhookWindowsHookEx(this._keyboardHookId);
            this._keyboardHookId = nint.Zero;
        } else {
            SimpleLog.Warning("Got disable with hook set to zero!");
        }

        this._cts?.Cancel();
        
        base.Disable();
    }

    public override void Dispose() {
        this._cts?.Dispose();

        base.Dispose();
    }

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.TextColored(ImGuiColors.DalamudRed, "WARNING: ");
        ImGui.SameLine();
        ImGui.TextWrapped("This Tweak will allow FFXIV to read all keystrokes from your computer while it is " +
                          "running. While care has been taken to ensure that this is done safely (and the game does " +
                          "nothing when it's not in focus), this may trigger certain antivirus or security software. " +
                          "In rare conditions, this Tweak may also cause input lag in other applications while FFXIV " +
                          "is open.");
        
        ImGui.Spacing();
        
        if (ImGui.Checkbox("Block Alt-Tab", ref this.Config.BlockAltTab)) {
            hasChanged = true;
        }

        if (ImGui.Checkbox("Block Windows Key", ref this.Config.BlockWinKey)) {
            hasChanged = true;
        }

        if (ImGui.Checkbox("Block Ctrl-Esc", ref this.Config.BlockCtrlEsc)) {
            hasChanged = true;
        }
        
        if (ImGui.Checkbox("Block Caps Lock", ref this.Config.BlockCapsLock)) {
            hasChanged = true;
        }

        ImGui.Spacing();

        if (ImGui.Checkbox("Only Apply In Combat", ref this.Config.InCombatOnly)) {
            hasChanged = true;
        }
    }

    private nint OnKeystroke(int nCode, nint wParam, ref KeyInfoStruct lParam) {
        // DANGER: This method is *highly sensitive* to performance impacts! Keep it light!!
        // When this tweak runs, this method runs on *every keyboard event across the entire system*. As such, if this
        // takes too long, it *will* be noticeable to the user, including if/when they're not in the game. Yes, this
        // does in fact turn FFXIV into a de facto keylogger. We have to do this to capture certain keys.

        if (!TryFindGameWindow(out var handle)) goto ORIGINAL;
        if (GetForegroundWindow() != handle) goto ORIGINAL;

        if (this.Config.InCombatOnly && !Service.Condition[ConditionFlag.InCombat]) goto ORIGINAL;

        switch ((VirtualKey) lParam.vkCode) {
            case VirtualKey.TAB when lParam.flags == KeyInfoFlags.LLKHF_ALTDOWN && this.Config.BlockAltTab:
            case VirtualKey.F4 when lParam.flags == KeyInfoFlags.LLKHF_ALTDOWN:
            case VirtualKey.ESCAPE when IsKeyDown(VirtualKey.CONTROL) && this.Config.BlockCtrlEsc:
            case VirtualKey.LWIN or VirtualKey.RWIN when this.Config.BlockWinKey:
            case VirtualKey.CAPITAL when this.Config.BlockCapsLock:
                // Send this keystroke to the game directly so it can be used as a keybind 
                SendMessageW(handle, lParam.flags == KeyInfoFlags.LLKHF_UP ? WM_KEYUP : WM_KEYDOWN, lParam.vkCode, 0);
                return 1;
        }

        ORIGINAL:
        return CallNextHookEx(this._keyboardHookId, nCode, wParam, ref lParam);
    }

    private static bool IsKeyDown(VirtualKey vKey) {
        return (GetAsyncKeyState((int) vKey) & 0x8000) > 0;
    }

    private static bool TryFindGameWindow(out nint handle) {
        handle = nint.Zero;
        while (true) {
            handle = FindWindowExW(nint.Zero, handle, "FFXIVGAME", null);
            if (handle == nint.Zero) break;
            var _ = GetWindowThreadProcessId(handle, out var pid);
            if (pid == Environment.ProcessId) break;
        }

        return handle != nint.Zero;
    }
}