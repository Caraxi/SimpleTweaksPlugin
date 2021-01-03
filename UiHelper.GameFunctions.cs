
using System;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin
{
    public unsafe partial class UiHelper
    {
        
        private delegate void AtkTextNodeSetText(AtkTextNode* textNode, void* a2);
        private static AtkTextNodeSetText atkTextNodeSetText;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);
        private static GameAlloc gameAlloc;

        private delegate IntPtr GetGameAllocator();
        private static GetGameAllocator getGameAllocator;

        public static bool Ready = false;
        public static void Setup(SigScanner scanner) {
            atkTextNodeSetText = Marshal.GetDelegateForFunctionPointer<AtkTextNodeSetText>(scanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC"));
            gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(scanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23"));
            getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08"));

            Ready = true;
        }

        public static IntPtr Alloc(ulong size) {
            if (gameAlloc == null || getGameAllocator == null) return IntPtr.Zero;
            return gameAlloc(size, IntPtr.Zero, getGameAllocator(), IntPtr.Zero);
        }
    }
}
