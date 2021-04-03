using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.GameStructs {
    
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct InventoryItem {

        [FieldOffset(0x00)] public InventoryType Container;
        [FieldOffset(0x04)] public short Slot;
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public uint Quantity;
        [FieldOffset(0x10)] public ushort Spiritbond;
        [FieldOffset(0x12)] public ushort Condition;
        [FieldOffset(0x14)] public ItemFlags Flags;
        [FieldOffset(0x20)] public ushort Materia0;
        [FieldOffset(0x22)] public ushort Materia1;
        [FieldOffset(0x24)] public ushort Materia2;
        [FieldOffset(0x26)] public ushort Materia3;
        [FieldOffset(0x28)] public ushort Materia4;
        [FieldOffset(0x2A)] public byte MateriaLevel0;
        [FieldOffset(0x2B)] public byte MateriaLevel1;
        [FieldOffset(0x2C)] public byte MateriaLevel2;
        [FieldOffset(0x2D)] public byte MateriaLevel3;
        [FieldOffset(0x2E)] public byte MateriaLevel4;
        [FieldOffset(0x2F)] public byte Stain;
        [FieldOffset(0x30)] public uint GlamourId;
        public bool IsHQ => (Flags & ItemFlags.HQ) == ItemFlags.HQ;

        public IEnumerable<(ushort materiaId, byte level)> Materia() {
            if (Materia0 != 0) yield return (Materia0, MateriaLevel0); else yield break;
            if (Materia1 != 0) yield return (Materia1, MateriaLevel1); else yield break;
            if (Materia2 != 0) yield return (Materia2, MateriaLevel2); else yield break;
            if (Materia3 != 0) yield return (Materia3, MateriaLevel3); else yield break;
            if (Materia4 != 0) yield return (Materia4, MateriaLevel4);
        }
        
        public Item Item => Common.PluginInterface.Data.Excel.GetSheet<Item>().GetRow(this.ItemId);
    }

    [Flags]
    public enum ItemFlags : byte {
        None,
        HQ,
    }
    
}
