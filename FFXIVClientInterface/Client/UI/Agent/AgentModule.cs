using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentModule : StructWrapper<AgentModuleStruct> {
        public static implicit operator AgentModuleStruct*(AgentModule module) => module.Data;
        public static explicit operator ulong(AgentModule module) => (ulong) module.Data;
        public static explicit operator AgentModule(AgentModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentModule(void* ptr) => new() {Data = (AgentModuleStruct*) ptr};

        private delegate void* GetAgentByInternalID(AgentModuleStruct* agentModule, AgentId agentId);
        private GetAgentByInternalID getAgentByInternalID;

        public void* GetAgentByID(AgentId agentId) {
            getAgentByInternalID ??= Marshal.GetDelegateForFunctionPointer<GetAgentByInternalID>(ClientInterface.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 FF 0D"));
            return getAgentByInternalID(this, agentId);
        }
        
        public T GetAgent<T>() where T : IAgent, new() {
            var agent = new T();
            var ptr = GetAgentByID(agent.AgentID);
            if (ptr == null) return default;
            agent.SetData(ptr);
            return agent;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public unsafe struct AgentModuleStruct {
        [FieldOffset(0x0)] public void* vtbl;
    }

}

