using System.Runtime.InteropServices;

namespace FFXIVClientInterface.Client.UI.Agent {
    public unsafe class AgentMarket : AbstractAgent<AgentMarketStruct> {
        public override AgentId AgentID => AgentId.Market;
        
        public static implicit operator AgentMarketStruct*(AgentMarket module) => module.Data;
        public static explicit operator ulong(AgentMarket module) => (ulong) module.Data;
        public static explicit operator AgentMarket(AgentMarketStruct* @struct) => new() {Data = @struct};
        public static explicit operator AgentMarket(void* ptr) => new() {Data = (AgentMarketStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x31A8)]
    public unsafe struct AgentMarketStruct {
        [FieldOffset(0x0)] public void* vtbl;
        [FieldOffset(0x2CC4)] public uint MarketResultItemId;
        [FieldOffset(0x2CDC)] public uint MarketResultSelectedIndex;
        [FieldOffset(0x2CE4)] public ushort MarketResultSelectedQuantity;
    }
}

