using System;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.GameStructs.Client.UI {
    
    [StructLayout(LayoutKind.Explicit, Size = 0x38)]
    public struct InventoryItem {

        [FieldOffset(0x00)] public int Container;
        [FieldOffset(0x04)] public short Slot;
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public uint Quantity;
        [FieldOffset(0x10)] public ushort Spiritbond;
        [FieldOffset(0x12)] public ushort Condition;
        [FieldOffset(0x14)] public ItemFlags Flags;
        [FieldOffset(0x30)] public uint GlamourId;

    }

    [Flags]
    public enum ItemFlags : byte {
        None,
        HQ,
    }
    
}
