using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Internal.Gui;
using FFXIVClientStructs.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.GameStructs.Client.UI.Client.UI.Misc;

namespace SimpleTweaksPlugin.Helper
{
    public unsafe partial class UiHelper {

        private delegate void AtkTextNodeSetText(AtkTextNode* textNode, void* a2);
        private static AtkTextNodeSetText _atkTextNodeSetText;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);
        private static GameAlloc _gameAlloc;

        private delegate IntPtr GetGameAllocator();
        private static GetGameAllocator _getGameAllocator;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UiModuleStruct* GetUiModuleDelegate();
        private static GetUiModuleDelegate _getUiModule;
        
        private delegate byte AtkUnitBaseClose(AtkUnitBase* unitBase, byte a2);
        private static AtkUnitBaseClose _atkUnitBaseClose;
        
        public static bool Ready = false;

        public static void Setup(SigScanner scanner) {
            _atkTextNodeSetText = Marshal.GetDelegateForFunctionPointer<AtkTextNodeSetText>(scanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC"));
            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(scanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23"));
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08"));
            _getUiModule = Marshal.GetDelegateForFunctionPointer<GetUiModuleDelegate>(scanner.ScanText("E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 52 40 80 88 ?? ?? ?? ?? 01 E9"));
            _atkUnitBaseClose = Marshal.GetDelegateForFunctionPointer<AtkUnitBaseClose>(scanner.ScanText("40 53 48 83 EC 50 81 A1"));

            Ready = true;
        }

        public static IntPtr Alloc(ulong size) {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }


        private static UiModule _uiModule;

        public static UiModule UiModule {
            get {
                if (_uiModule == null || !_uiModule.IsValid) {
                    var uiModule = _getUiModule();
                    if (uiModule != null) {
                        _uiModule = new UiModule() { Data = uiModule };
                    }
                }

                return _uiModule;
            }
        }

        
    }
}
