using System.Runtime.InteropServices;
using Dalamud.Game.Internal.Gui.Structs;

namespace SimpleTweaksPlugin.GameStructs {

    [StructLayout(LayoutKind.Explicit, Size = 0x2B8)]
    public unsafe struct AddonActionBarMain {
        [FieldOffset(0x000)] public AddonActionBarBase ActionBarBase;
        [FieldOffset(0x268)] public AtkResNode* ActionBarLockButton;
    }
}
