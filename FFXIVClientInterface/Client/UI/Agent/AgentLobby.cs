using System.Runtime.InteropServices;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentLobby : AbstractAgent<AgentLobbyStruct> {
        public override AgentId AgentID => AgentId.Lobby;
        
        public static implicit operator AgentLobbyStruct*(AgentLobby module) => module.Data;
        public static explicit operator ulong(AgentLobby module) => (ulong) module.Data;
        public static explicit operator AgentLobby(AgentLobbyStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentLobby(void* ptr) => new() {Data = (AgentLobbyStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xA90)]
    public unsafe struct AgentLobbyStruct {
        [FieldOffset(0x0)] public void* vtbl;
        [FieldOffset(0x820)] public byte DataCenter;
        [FieldOffset(0x840)] public uint IdleTime;
    }
}

