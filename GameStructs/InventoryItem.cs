using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTweaksPlugin.GameStructs {
    
    [StructLayout(LayoutKind.Explicit, Size = 0x38)]
    public struct InventoryItem {

        [FieldOffset(0x00)] public int Container;
        [FieldOffset(0x04)] public int Slot;
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public uint Quantity;
        [FieldOffset(0x10)] public ushort Spiritbond;
        [FieldOffset(0x12)] public ushort Condition;
        [FieldOffset(0x14)] public byte Flags;
        [FieldOffset(0x30)] public uint GlamourId;

    }
}
