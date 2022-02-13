using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.GameStructs; 

[StructLayout(LayoutKind.Explicit, Size = 0x2B8)]
public unsafe struct AddonActionBarMain {
    [FieldOffset(0x000)] public AddonActionBarBase ActionBarBase;
    [FieldOffset(0x268)] public AtkResNode* ActionBarLockButton;
}