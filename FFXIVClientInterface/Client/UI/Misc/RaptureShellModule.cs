using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class RaptureShellModule : StructWrapper<RaptureShellModuleStruct> {
        public static implicit operator RaptureShellModuleStruct*(RaptureShellModule module) => module.Data;
        public static explicit operator ulong(RaptureShellModule module) => (ulong) module.Data;
        public static explicit operator RaptureShellModule(RaptureShellModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator RaptureShellModule(void* ptr) => new() {Data = (RaptureShellModuleStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1208)]
    public unsafe struct RaptureShellModuleStruct {
        [FieldOffset(0x0)] public void* vtbl;
        
        [FieldOffset(0x2C0)] public int MacroCurrentLine;
        [FieldOffset(0x2B3)] public byte MacroLockState;
    }

}

