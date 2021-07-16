using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class RaptureAtkModule : StructWrapper<RaptureAtkModuleStruct> {
        public static implicit operator RaptureAtkModuleStruct*(RaptureAtkModule module) => module.Data;
        public static explicit operator ulong(RaptureAtkModule module) => (ulong) module.Data;
        public static explicit operator RaptureAtkModule(RaptureAtkModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator RaptureAtkModule(void* ptr) => new() {Data = (RaptureAtkModuleStruct*) ptr};

        public FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule.NamePlateInfo* NamePlateInfo => &Data->RaptureAtkModule.NamePlateInfoArray;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x27718)]
    public unsafe struct RaptureAtkModuleStruct {
        [FieldOffset(0x0)] public void* vtbl;
        [FieldOffset(0x0)] public FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule RaptureAtkModule;
    }

}

