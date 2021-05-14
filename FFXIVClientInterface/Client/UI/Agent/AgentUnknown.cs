using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentUnknown : AbstractAgent<AgentUnknownStruct> {
        public static implicit operator AgentUnknownStruct*(AgentUnknown module) => module.Data;
        public static explicit operator ulong(AgentUnknown module) => (ulong) module.Data;
        public static explicit operator AgentUnknown(AgentUnknownStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentUnknown(void* ptr) => new() {Data = (AgentUnknownStruct*) ptr};
        public override AgentId AgentID => AgentId.Unknown;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public unsafe struct AgentUnknownStruct {
        [FieldOffset(0x0)] public void* vtbl;
        [FieldOffset(0x0)] public void** vfunc;
    }

}

