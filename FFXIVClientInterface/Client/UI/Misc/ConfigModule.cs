using System.Runtime.InteropServices;
using Dalamud.Plugin;
using FFXIVClientInterface.Misc;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace FFXIVClientInterface.Client.UI.Misc {
    public unsafe class ConfigModule : StructWrapper<ConfigModuleStruct> {
        public static implicit operator ConfigModuleStruct*(ConfigModule module) => module.Data;
        public static explicit operator ulong(ConfigModule module) => (ulong) module.Data;
        public static explicit operator ConfigModule(ConfigModuleStruct* @struct) => new() {Data = @struct};
        public static explicit operator ConfigModule(void* ptr) => new() {Data = (ConfigModuleStruct*) ptr};

        public bool GetOptionBoolean(ulong index) => this.Data->OptionCache[index * 16] != 0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xD678)]
    public unsafe struct ConfigModuleStruct {

        public const int ConfigOptionCount = 674;
        
        [FieldOffset(0x0000)] public void* vtbl;

        [FieldOffset(0x0088)] public Utf8String UnknownString_0088;
        [FieldOffset(0x00F0)] public Utf8String UnknownString_00F0;
        [FieldOffset(0x0190)] public Utf8String UnknownString_0190;
        
        [FieldOffset(0xAB60)] public fixed byte OptionCache[ConfigOptionCount * 16];
    }

}

