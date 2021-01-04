using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.Component.GUI;

namespace SimpleTweaksPlugin.Helper
{
    public unsafe partial class UiHelper
    {
        
        private delegate void AtkTextNodeSetText(AtkTextNode* textNode, void* a2);
        private static AtkTextNodeSetText _atkTextNodeSetText;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);
        private static GameAlloc _gameAlloc;

        private delegate IntPtr GetGameAllocator();
        private static GetGameAllocator _getGameAllocator;

        public static bool Ready = false;
        public static void Setup(SigScanner scanner) {
            _atkTextNodeSetText = Marshal.GetDelegateForFunctionPointer<AtkTextNodeSetText>(scanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC"));
            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(scanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23"));
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08"));

            Ready = true;
        }

        public static IntPtr Alloc(ulong size) {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }
    }
}
