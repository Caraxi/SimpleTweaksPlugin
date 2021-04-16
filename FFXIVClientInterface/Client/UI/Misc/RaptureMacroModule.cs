using System.Drawing;
using System.Runtime.InteropServices;
using FFXIVClientInterface.Misc;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class RaptureMacroModule : StructWrapper<RaptureMacroModuleStruct> {
        public static implicit operator RaptureMacroModuleStruct*(RaptureMacroModule module) => module.Data;
        public static explicit operator ulong(RaptureMacroModule module) => (ulong) module.Data;
        public static explicit operator RaptureMacroModule(RaptureMacroModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator RaptureMacroModule(void* ptr) => new() {Data = (RaptureMacroModuleStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x51AA8)]
    public unsafe struct RaptureMacroModuleStruct {

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        public struct Macro {
            
            public struct LinesStruct {
                public Utf8String Line01;
                public Utf8String Line02;
                public Utf8String Line03;
                public Utf8String Line04;
                public Utf8String Line05;
                public Utf8String Line06;
                public Utf8String Line07;
                public Utf8String Line08;
                public Utf8String Line09;
                public Utf8String Line10;
                public Utf8String Line11;
                public Utf8String Line12;
                public Utf8String Line13;
                public Utf8String Line14;
                public Utf8String Line15;

                public Utf8String this[int i] {
                    get {
                        return i switch {
                            00 => Line01, 01 => Line02, 02 => Line03,
                            03 => Line04, 04 => Line05, 05 => Line06,
                            06 => Line07, 07 => Line08, 08 => Line09,
                            09 => Line10, 10 => Line11, 11 => Line12,
                            12 => Line13, 13 => Line14, 14 => Line15,
                            _ => default,
                        };
                    }
                }
            }

            public const int Size = 0x688;
            public Utf8String Name;
            public LinesStruct Line;
        }

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        public struct MacroPage {
            public const int Size = Macro.Size * 100;

            private fixed byte data[Size];

            public Macro* this[int i] {
                get {
                    if (i < 0 || i > 99) return null;
                    Macro* a;
                    fixed (byte* p = this.data) {
                        a = (Macro*) (p + Macro.Size * i);
                    }
                    return a;
                }
            }
        }
        
        
        
        [FieldOffset(0x0)] public void* vtbl;

        [FieldOffset(0x58)] public MacroPage Individual;
        [FieldOffset(0x58 + MacroPage.Size)] public MacroPage Shared;
    }

}

