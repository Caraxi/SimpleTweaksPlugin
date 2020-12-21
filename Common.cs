using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Plugin;
using FFXIVClientStructs;

namespace SimpleTweaksPlugin {
    internal class Common {

        private readonly DalamudPluginInterface pluginInterface;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);

        private delegate IntPtr GetGameAllocator();

        private static GameAlloc gameAlloc;
        private static GetGameAllocator getGameAllocator;
        


        public Common(DalamudPluginInterface pluginInterface) {
            this.pluginInterface = pluginInterface;
            var gameAllocPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23");
            var getGameAllocatorPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08");
            gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(gameAllocPtr);
            getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(getGameAllocatorPtr);
        }

        public static IntPtr Alloc(ulong size) {
            if (gameAlloc == null || getGameAllocator == null) return IntPtr.Zero;
            return gameAlloc(size, IntPtr.Zero, getGameAllocator(), IntPtr.Zero);
        }
        
        public unsafe void WriteSeString(byte** startPtr, IntPtr alloc, SeString seString) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*)alloc) return;
            WriteSeString((byte*)alloc, seString);
            *startPtr = (byte*)alloc;
        }

        public unsafe SeString ReadSeString(byte** startPtr) {
            if (startPtr == null) return null;
            var start = *(startPtr);
            if (start == null) return null;
            return ReadSeString(start);
        }

        public unsafe SeString ReadSeString(byte* ptr) {
            var offset = 0;
            while (true) {
                var b = *(ptr + offset);
                if (b == 0) {
                    break;
                }
                offset += 1;
            }

            var bytes = new byte[offset];
            Marshal.Copy(new IntPtr(ptr), bytes, 0, offset);

            return pluginInterface.SeStringManager.Parse(bytes);
        }

        public unsafe void WriteSeString(byte* dst, SeString s) {
            var bytes = s.Encode();
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            *(dst + bytes.Length) = 0;
        }

        public unsafe void WriteSeString(FFXIVString xivString, SeString s) {
            var bytes = s.Encode();
            int i;
            for (i = 0; i < bytes.Length && i < xivString.BufSize - 1; i++) {
                *(xivString.StringPtr + i) = bytes[i];
            }
            *(xivString.StringPtr + i) = 0;
        }

        public enum GameOptionKind : uint {
            GamePadMode        = 0x089, // [bool] Character Config -> Mouse Mode / GamePad Mode
            LegacyMovement     = 0x08A, // [bool] Character Config -> Control Settings -> General -> Standard Type / Legacy Type
            DisplayItemHelp    = 0x130, // [bool] Character Config -> UI Settings -> General -> Display Item Help
            DisplayActionHelp  = 0x136, // [bool] Character Config -> UI Settings -> General -> Display Action Help

            ClockDisplayType   = 0x153, // [enum/byte] 0 = Default, 1 = 24H, 2 = 12H 
            ClockTypeEorzea    = 0x155, // [bool]
            ClockTypeLocal     = 0x156, // [bool]
            ClockTypeServer    = 0x157, // [bool]
        }


        public unsafe T GetGameOption<T>(GameOptionKind opt) {
            var optionBase = (byte**)(pluginInterface.Framework.Address.BaseAddress + 0x2B28);
            return Marshal.PtrToStructure<T>(new IntPtr(*optionBase + 0xAAE0 + (16 * (uint)opt)));
        }
    }
}
