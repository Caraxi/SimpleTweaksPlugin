using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.GameStructs {
    
    [StructLayout(LayoutKind.Explicit, Size = 0x348)]
    public struct AddonActionDetail {
        [FieldOffset(0x000)] public AtkUnitBase AtkUnitBase;
        
    }
}
