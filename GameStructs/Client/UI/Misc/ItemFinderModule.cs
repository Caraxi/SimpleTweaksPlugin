using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.GameStructs.Client.UI.Misc {
    public unsafe class ItemFinderModule : StructWrapper<ItemFinderModuleStruct> {
        public static implicit operator ItemFinderModuleStruct*(ItemFinderModule module) => module.Data;
        public static explicit operator ulong(ItemFinderModule module) => (ulong) module.Data;
        public static explicit operator ItemFinderModule(ItemFinderModuleStruct* @struct) => new() { Data = @struct };
        public static explicit operator ItemFinderModule(void* ptr) => new() { Data = (ItemFinderModuleStruct*) ptr};
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB90)]
    public unsafe struct ItemFinderModuleStruct {
        [FieldOffset(0x0000)] public void* vtbl;
    }
}

