using System;
using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace FFXIVClientInterface.Client.UI.Misc {

    [Flags]
    public enum HotBarType : byte {
        None = 0x00,
        Normal = 0x01,
        Cross = 0x02,
        NormalPet = 0x4,
        CrossPet = 0x08,
        
        NormalWithPet = Normal | NormalPet,
        CrossWithPet = Cross | CrossPet,
        Pet = NormalPet | CrossPet,
        All = Normal | Cross,
        AllWithPet = All | Pet,
    }

    public unsafe class RaptureHotbarModule : StructWrapper<RaptureHotbarModuleStruct> {
        public static implicit operator RaptureHotbarModuleStruct*(RaptureHotbarModule module) => module.Data;
        public static explicit operator ulong(RaptureHotbarModule module) => (ulong) module.Data;
        public static explicit operator RaptureHotbarModule(RaptureHotbarModuleStruct* @struct) => new() { Data = @struct };
        public static explicit operator RaptureHotbarModule(void* ptr) => new() { Data = (RaptureHotbarModuleStruct*) ptr};
        
        public HotBar* GetBar(int index, HotBarType type = HotBarType.AllWithPet) {
            if (index < 0) return null;
            if ((type & HotBarType.Normal) != HotBarType.None) {
                    
                if (--index < 0) return &Data->HotBar01;
                if (--index < 0) return &Data->HotBar02;
                if (--index < 0) return &Data->HotBar03;
                if (--index < 0) return &Data->HotBar04;
                if (--index < 0) return &Data->HotBar05;
                if (--index < 0) return &Data->HotBar06;
                if (--index < 0) return &Data->HotBar07;
                if (--index < 0) return &Data->HotBar08;
                if (--index < 0) return &Data->HotBar09;
                if (--index < 0) return &Data->HotBar10;
            }

            if ((type & HotBarType.Cross) != HotBarType.None) {
                if (--index < 0) return &Data->CrossHotBar01;
                if (--index < 0) return &Data->CrossHotBar02;
                if (--index < 0) return &Data->CrossHotBar03;
                if (--index < 0) return &Data->CrossHotBar04; 
                if (--index < 0) return &Data->CrossHotBar05;
                if (--index < 0) return &Data->CrossHotBar06;
                if (--index < 0) return &Data->CrossHotBar07;
                if (--index < 0) return &Data->CrossHotBar08;
            }

            if ((type & HotBarType.NormalPet) != HotBarType.None) {
                if (--index < 0) return &Data->PetHotBar;
            }
                
            if ((type & HotBarType.CrossPet) != HotBarType.None) {
                if (--index < 0) return &Data->CrossPetHotBar;
            }

            return null;
        }

        public int GetBarCount(HotBarType type = HotBarType.All) {
            var count = 0;
            if ((type & HotBarType.Normal) != HotBarType.None) count += 10;
            if ((type & HotBarType.Cross) != HotBarType.None) count += 8;
            if ((type & HotBarType.NormalPet) != HotBarType.None) count++;
            if ((type & HotBarType.CrossPet) != HotBarType.None) count++;
            return count;
        }

        public HotBarSlot* GetBarSlot(HotBar* bar, int slotIndex) {
            if (bar == null) return null;
            return slotIndex switch {
                00 => &bar->S00, 01 => &bar->S01, 02 => &bar->S02, 03 => &bar->S03,
                04 => &bar->S04, 05 => &bar->S05, 06 => &bar->S06, 07 => &bar->S07,
                08 => &bar->S08, 09 => &bar->S09, 10 => &bar->S10, 11 => &bar->S11,
                12 => &bar->S12, 13 => &bar->S13, 14 => &bar->S14, 15 => &bar->S15,
                _ => null
            };
        }

    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0x25BF0)]
    public unsafe struct RaptureHotbarModuleStruct {
        [FieldOffset(0x00000)] public void* vtbl;
        
        [FieldOffset(0x00030)] public fixed byte ModuleName[16];
        
        [FieldOffset(0x00088 + HotBar.Size * 00)] public HotBar HotBar01;
        [FieldOffset(0x00088 + HotBar.Size * 01)] public HotBar HotBar02;
        [FieldOffset(0x00088 + HotBar.Size * 02)] public HotBar HotBar03;
        [FieldOffset(0x00088 + HotBar.Size * 03)] public HotBar HotBar04;
        [FieldOffset(0x00088 + HotBar.Size * 04)] public HotBar HotBar05;
        [FieldOffset(0x00088 + HotBar.Size * 05)] public HotBar HotBar06;
        [FieldOffset(0x00088 + HotBar.Size * 06)] public HotBar HotBar07;
        [FieldOffset(0x00088 + HotBar.Size * 07)] public HotBar HotBar08;
        [FieldOffset(0x00088 + HotBar.Size * 08)] public HotBar HotBar09;
        [FieldOffset(0x00088 + HotBar.Size * 09)] public HotBar HotBar10;

        [FieldOffset(0x00088 + HotBar.Size * 10)] public HotBar CrossHotBar01;
        [FieldOffset(0x00088 + HotBar.Size * 11)] public HotBar CrossHotBar02;
        [FieldOffset(0x00088 + HotBar.Size * 12)] public HotBar CrossHotBar03;
        [FieldOffset(0x00088 + HotBar.Size * 13)] public HotBar CrossHotBar04;
        [FieldOffset(0x00088 + HotBar.Size * 14)] public HotBar CrossHotBar05;
        [FieldOffset(0x00088 + HotBar.Size * 15)] public HotBar CrossHotBar06;
        [FieldOffset(0x00088 + HotBar.Size * 16)] public HotBar CrossHotBar07;
        [FieldOffset(0x00088 + HotBar.Size * 17)] public HotBar CrossHotBar08;

        [FieldOffset(0x0FC88)] public HotBar PetHotBar;
        [FieldOffset(0x10A88)] public HotBar CrossPetHotBar;
    }

    [StructLayout(LayoutKind.Sequential, Size = HotBar.Size)]
    public unsafe struct HotBar {
        public const int Size = 0xE00;
        public HotBarSlot S00;
        public HotBarSlot S01;
        public HotBarSlot S02;
        public HotBarSlot S03;
        public HotBarSlot S04;
        public HotBarSlot S05;
        public HotBarSlot S06;
        public HotBarSlot S07;
        public HotBarSlot S08;
        public HotBarSlot S09;
        public HotBarSlot S10;
        public HotBarSlot S11;
        public HotBarSlot S12;
        public HotBarSlot S13;
        public HotBarSlot S14;
        public HotBarSlot S15;

        public int Count => 16;
        public HotBarSlot this[int index] {
            get {
                if (index < 0) throw new IndexOutOfRangeException("index must be positive");
                if (--index < 0) return this.S00;
                if (--index < 0) return this.S01;
                if (--index < 0) return this.S02;
                if (--index < 0) return this.S03;
                if (--index < 0) return this.S04;
                if (--index < 0) return this.S05;
                if (--index < 0) return this.S06;
                if (--index < 0) return this.S07;
                if (--index < 0) return this.S08;
                if (--index < 0) return this.S09;
                if (--index < 0) return this.S10;
                if (--index < 0) return this.S11;
                if (--index < 0) return this.S12;
                if (--index < 0) return this.S13;
                if (--index < 0) return this.S14;
                if (--index < 0) return this.S15;
                throw new IndexOutOfRangeException("index must be no greater than 15");
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xE0)]
    public unsafe struct HotBarSlot {
        [FieldOffset(0x00)] public Utf8String PopUpHelp;
        // [FieldOffset(0x68)]
        
        [FieldOffset(0xB8)] public uint CommandId;
        [FieldOffset(0xBC)] public uint IconA;
        [FieldOffset(0xC0)] public uint IconB;
        
        [FieldOffset(0xC7)] public HotbarSlotType CommandType;
        [FieldOffset(0xC8)] public HotbarSlotType IconTypeA;
        [FieldOffset(0xC9)] public HotbarSlotType IconTypeB;
        
        [FieldOffset(0xCC)] public uint Icon;
        


        [FieldOffset(0xDF)] public byte IsEmpty; // ?
    }

    public enum HotbarSlotType : byte { 
        Empty = 0x00,
        Action = 0x01,
        Item = 0x02,
        
        EventItem = 0x04,
        
        Emote = 0x06,
        Macro = 0x07,
        Marker = 0x08,
        CraftAction = 0x09,
        GeneralAction = 0x0A,
        CompanionOrder = 0x0B,
        MainCommand = 0x0C,
        Minion = 0x0D,
        
        GearSet = 0x0F,
        PetOrder = 0x10,
        Mount = 0x11,
        FieldMarker = 0x12,
        
        Recipe = 0x14,
        
        ExtraCommand = 0x18,
        PvPQuickChat = 0x19,
        PvPCombo = 0x1A,
        SquadronOrder = 0x1B,
        
        PerformanceInstrument = 0x1D,
        Collection = 0x1E,
        FashionAccessory = 0x1F,
        LostFindsItem = 0x20,
    }
}
