using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Utility; 

public unsafe partial class UiHelper {
    private delegate byte AtkUnitBaseClose(AtkUnitBase* unitBase, byte a2);
    private static AtkUnitBaseClose _atkUnitBaseClose;
        
    public static bool Ready;

    public static void Setup(ISigScanner scanner) {
        _atkUnitBaseClose = Marshal.GetDelegateForFunctionPointer<AtkUnitBaseClose>(scanner.ScanText("40 53 48 83 EC 50 81 A1"));
        Ready = true;
    }

    public static IntPtr Alloc(ulong size) {
        return new IntPtr(IMemorySpace.GetUISpace()->Malloc(size, 8UL));
    }

    public static IntPtr Alloc(int size) {
        if (size <= 0) throw new ArgumentException("Allocation size must be positive.");
        return Alloc((ulong) size);
    }
}