using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe interface IAgent {
        public AgentId AgentID { get; }
        public void SetData(void* data);
    }
    public abstract class AbstractAgent<T> : StructWrapper<T>, IAgent where T : unmanaged {
        public abstract AgentId AgentID { get; }
    }
}
