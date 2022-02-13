using System.Runtime.InteropServices;
using SimpleTweaksPlugin.Enums;

namespace SimpleTweaksPlugin.GameStructs; 

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public unsafe struct InventoryContainer {
    [FieldOffset(0x00)] public InventoryItem* Items;
    [FieldOffset(0x08)] public InventoryType Type;
    [FieldOffset(0x0C)] public int SlotCount;
    [FieldOffset(0x10)] public byte Loaded;
}