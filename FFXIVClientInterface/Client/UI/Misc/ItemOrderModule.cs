using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class ItemOrderModule : StructWrapper<ItemOrderModuleStruct> {
        public static implicit operator ItemOrderModuleStruct*(ItemOrderModule module) => module.Data;
        public static explicit operator ulong(ItemOrderModule module) => (ulong) module.Data;
        public static explicit operator ItemOrderModule(ItemOrderModuleStruct* @struct) => new() { Data = @struct };
        public static explicit operator ItemOrderModule(void* ptr) => new() { Data = (ItemOrderModuleStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xD8)]
    public unsafe struct ItemOrderModuleStruct {
        [FieldOffset(0x00)] public void* vtbl;
        [FieldOffset(0x30)] public fixed byte ModuleName[16];
        [FieldOffset(0x40)] public ItemOrderContainer* PlayerInventory;
        [FieldOffset(0x48)] public ItemOrderArmoury Armoury;

        [FieldOffset(0xB0)] public ulong RetainerID;
        [FieldOffset(0xB8)] public void* RetainerPtr;
        [FieldOffset(0xC0)] public uint RetainerCount;
        
        [FieldOffset(0xC8)] public ItemOrderContainer* SaddleBagLeft;
        [FieldOffset(0xD0)] public ItemOrderContainer* SaddleBagRight;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x68)]
    public unsafe struct ItemOrderArmoury {
        public ItemOrderContainer* MainHand;
        public ItemOrderContainer* Head;
        public ItemOrderContainer* Body;
        public ItemOrderContainer* Hands;
        public ItemOrderContainer* Waist;
        public ItemOrderContainer* Legs;
        public ItemOrderContainer* Feet;
        public ItemOrderContainer* OffHand;
        public ItemOrderContainer* Ears;
        public ItemOrderContainer* Neck;
        public ItemOrderContainer* Wrists;
        public ItemOrderContainer* Rings;
        public ItemOrderContainer* SoulCrystal;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x60)]
    public unsafe struct ItemOrderContainer {
        [FieldOffset(0x00)] public uint containerId;
        [FieldOffset(0x04)] public uint Unk04;
        [FieldOffset(0x08)] public void* Unk08;
        [FieldOffset(0x10)] public void* Unk10;
        [FieldOffset(0x18)] public void* Unk18;
        [FieldOffset(0x20)] public void* Unk20;
        [FieldOffset(0x28)] public uint SlotPerContainer;
        [FieldOffset(0x2C)] public uint Unk2C;
        [FieldOffset(0x30)] public void* Unk30;
        [FieldOffset(0x38)] public int Unk38;
        [FieldOffset(0x3C)] public int Unk3C;
        [FieldOffset(0x40)] public void* Unk40;
        [FieldOffset(0x48)] public void* Unk48;
        [FieldOffset(0x50)] public void* Unk50;
        [FieldOffset(0x58)] public int Unk58;
        [FieldOffset(0x5C)] public int Unk5C;

    }
    

}

