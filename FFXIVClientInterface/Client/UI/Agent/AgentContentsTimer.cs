using System.Runtime.InteropServices;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentContentsTimer : AbstractAgent<AgentContentsTimerStruct> {
        public static implicit operator AgentContentsTimerStruct*(AgentContentsTimer module) => module.Data;
        public static explicit operator ulong(AgentContentsTimer module) => (ulong) module.Data;
        public static explicit operator AgentContentsTimer(AgentContentsTimerStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentContentsTimer(void* ptr) => new() {Data = (AgentContentsTimerStruct*) ptr};
        public override AgentId AgentID => AgentId.ContentsTimer;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public unsafe struct AgentContentsTimerStruct {
        [FieldOffset(0x0)] public void* vtbl;
        [FieldOffset(0x0)] public void** vfunc;
    }

}

