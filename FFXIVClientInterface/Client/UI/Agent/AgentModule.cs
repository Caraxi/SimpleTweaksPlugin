using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentModule : StructWrapper<AgentModuleStruct> {
        public static implicit operator AgentModuleStruct*(AgentModule module) => module.Data;
        public static explicit operator ulong(AgentModule module) => (ulong) module.Data;
        public static explicit operator AgentModule(AgentModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentModule(void* ptr) => new() {Data = (AgentModuleStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public unsafe struct AgentModuleStruct {
        [FieldOffset(0x0)] public void* vtbl;
    }

}

