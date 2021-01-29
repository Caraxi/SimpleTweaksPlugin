using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Helper
{
    public unsafe partial class UiHelper {

        private delegate void AtkTextNodeSetText(AtkTextNode* textNode, void* a2);
        private static AtkTextNodeSetText _atkTextNodeSetText;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);
        private static GameAlloc _gameAlloc;

        private delegate IntPtr GetGameAllocator();
        private static GetGameAllocator _getGameAllocator;
        
        private delegate byte AtkUnitBaseClose(AtkUnitBase* unitBase, byte a2);
        private static AtkUnitBaseClose _atkUnitBaseClose;

        private delegate AtkResNode* CreateAtkNode(void* unused, NodeType type);
        private static CreateAtkNode _createAtkNode;
        
        public static bool Ready = false;

        public static void Setup(SigScanner scanner) {
            _atkTextNodeSetText = Marshal.GetDelegateForFunctionPointer<AtkTextNodeSetText>(scanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC"));
            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(scanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23"));
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08"));
            _atkUnitBaseClose = Marshal.GetDelegateForFunctionPointer<AtkUnitBaseClose>(scanner.ScanText("40 53 48 83 EC 50 81 A1"));
            _createAtkNode = Marshal.GetDelegateForFunctionPointer<CreateAtkNode>(scanner.ScanText("E8 ?? ?? ?? ?? 48 8B 4C 24 ?? 48 8B 51 08"));
            Ready = true;
        }

        public static IntPtr Alloc(ulong size) {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }

        public static IntPtr Alloc(int size) {
            if (size <= 0) throw new ArgumentException("Allocation size must be positive.");
            return Alloc((ulong) size);
        }

    }
}
