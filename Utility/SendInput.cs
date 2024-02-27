using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;

namespace SimpleTweaksPlugin.Utility;

public static class SendInput {
    [DllImport("user32.dll", EntryPoint = "SendInput")]
    private static extern uint NativeSendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] Input[] pInputs, int cbSize);

    public static void KeyDown(params VirtualKey[] keys) {
        if (keys.Length is <= 0 or > 10) throw new Exception("Invalid key combination");
        var inputs = new Input[keys.Length];
        for (var i = 0; i < keys.Length; i++) {
            inputs[i] = new Input { type = 1 };
            inputs[i].U.ki.wVk = keys[i];
        }
        
        NativeSendInput((ushort)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    public static void KeyUp(params VirtualKey[] keys) {
        if (keys.Length is <= 0 or > 10) throw new Exception("Invalid key combination");
        var inputs = new Input[keys.Length];
        for (var i = 0; i < keys.Length; i++) {
            inputs[i] = new Input { type = 1 };
            inputs[i].U.ki.wVk = keys[i];
            inputs[i].U.ki.dwFlags = KeyEvent.KeyUp;
        }
        
        NativeSendInput((ushort)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }
    
    
    [StructLayout(LayoutKind.Sequential)]
    private struct Input {
        internal uint type;
        internal InputUnion U;
    }

    // Declare the InputUnion struct
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion {
        [FieldOffset(0)] internal MouseInput mi;
        [FieldOffset(0)] internal KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput {
        internal int dx;
        internal int dy;
        internal uint mouseData;
        internal uint dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput {
        internal VirtualKey wVk;
        internal ushort wScan;
        internal KeyEvent dwFlags;
        internal int time;
        internal UIntPtr dwExtraInfo;
    }

    [Flags]
    private enum KeyEvent : uint {
        KeyUp = 0x0002
    }
}
