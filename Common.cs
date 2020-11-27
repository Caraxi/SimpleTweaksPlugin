using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin {
    internal class Common {

        private readonly DalamudPluginInterface pluginInterface;

        public Common(DalamudPluginInterface pluginInterface) {
            this.pluginInterface = pluginInterface;
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

        private unsafe SeString ReadSeString(byte* ptr) {
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

        private unsafe void WriteSeString(byte* dst, SeString s) {
            var bytes = s.Encode();
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            *(dst + bytes.Length) = 0;
        }


    }
}
