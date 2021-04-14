using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace SimpleTweaksPlugin.GameStructs {

    [StructLayout(LayoutKind.Explicit, Size = 0x298)]
    [Addon("_ActionBar")]
    public unsafe struct AddonActionBar {
        [FieldOffset(0x000)] public AddonActionBarBase ActionBarBase;
        [FieldOffset(0x248)] public void* UnknownPtr248;
        [FieldOffset(0x250)] public void* UnknownPtr250;
        [FieldOffset(0x258)] public void* UnknownPtr258;
        [FieldOffset(0x260)] public void* UnknownPtr260;
        [FieldOffset(0x268)] public void* UnknownPtr268;
        [FieldOffset(0x270)] public int UnknownInt270;
        [FieldOffset(0x274)] public byte UnknownByte274;
        [FieldOffset(0x27C)] public void* UnknownPtr27C;
        [FieldOffset(0x284)] public void* UnknownPtr284;
        [FieldOffset(0x28C)] public void* UnknownPtr28C;
    }
}
