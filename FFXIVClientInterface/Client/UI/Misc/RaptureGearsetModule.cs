using System;
using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class RaptureGearsetModule : StructWrapper<RaptureGearsetModuleStruct> {
        public static implicit operator RaptureGearsetModuleStruct*(RaptureGearsetModule module) => module.Data;
        public static explicit operator ulong(RaptureGearsetModule module) => (ulong) module.Data;
        public static explicit operator RaptureGearsetModule(RaptureGearsetModuleStruct* @struct) => new() { Data = @struct };
        public static explicit operator RaptureGearsetModule(void* ptr) => new() { Data = (RaptureGearsetModuleStruct*) ptr};
        public Gearset* Gearset => (Gearset*) this.Data->GearsetData;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB148)]
    public unsafe struct RaptureGearsetModuleStruct {
        [FieldOffset(0x0000)] public void* vtbl;
        [FieldOffset(0x0030)] public fixed byte ModuleName[16];
        [FieldOffset(0x0040)] public ulong Unknown40;
        [FieldOffset(0x0048)] public fixed byte GearsetData[0xAF2C];
        [FieldOffset(0xAF74)] public uint UnknownAF74;
        [FieldOffset(0xAF78)] public byte UnknownAF78;

        [FieldOffset(0xAF7C)] public UnknownGearsetStruct UnknownAF7C;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1BC)]
    public unsafe struct Gearset {
        [FieldOffset(0x000)] public byte ID;
        [FieldOffset(0x001)] public fixed byte Name[0x2F];
        
        [FieldOffset(0x32)] public byte GlamourSetLink;
        [FieldOffset(0x33)] public GearsetFlag Flags;

        [FieldOffset(0x34)] public fixed byte ItemsData[0x188];
        [FieldOffset(0x34)] public GearsetItem MainHand;
        [FieldOffset(0x50)] public GearsetItem OffHand;
        [FieldOffset(0x6C)] public GearsetItem Head;
        [FieldOffset(0x88)] public GearsetItem Body;
        [FieldOffset(0xA4)] public GearsetItem Hands;
        [FieldOffset(0xC0)] public GearsetItem Belt;
        [FieldOffset(0xDC)] public GearsetItem Legs;
        [FieldOffset(0xF8)] public GearsetItem Feet;
        [FieldOffset(0x114)] public GearsetItem Ears;
        [FieldOffset(0x130)] public GearsetItem Neck;
        [FieldOffset(0x14C)] public GearsetItem Wrists;
        [FieldOffset(0x168)] public GearsetItem RingRight;
        [FieldOffset(0x184)] public GearsetItem RightLeft;
        [FieldOffset(0x1A0)] public GearsetItem SoulStone;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x1C)]
    public unsafe struct GearsetItem {
        public uint ItemID;
    }

    [Flags]
    public enum GearsetFlag : byte {
        Exists = 0x01,
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0x264)]
    public unsafe struct UnknownGearsetStruct {
        
    }
    
}
