using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.GameStructs; 

[StructLayout(LayoutKind.Explicit, Size = 0x488)]
[Addon("_MiniTalk")]
public unsafe struct AddonMiniTalk {
    [FieldOffset(0x000)] public AtkUnitBase AtkUnitBase;
    [FieldOffset(0x220)] public void* Unk220; // Appears to be a function pointer
    [FieldOffset(0x228)] public AtkUnitBase* AddonScreenLog;
    [FieldOffset(0x230)] public void* Unk238;

    [FieldOffset(0x238)] public MiniTalkBubbles Bubbles;
        
    [FieldOffset(0x468)] public uint Unk468;
    [ValueParser.HexValue] [FieldOffset(0x46C)] public ulong Unk46C;
    [ValueParser.HexValue] [FieldOffset(0x474)] public ulong Unk474;
    [ValueParser.HexValue] [FieldOffset(0x47C)] public ulong Unk47C;
    [FieldOffset(0x484)] public uint Unk484;
        
} 
    
[StructLayout(LayoutKind.Sequential, Size = ByteSize)]
public unsafe struct MiniTalkBubble {
    public const int ByteSize = 0x38;
        
        
    public AtkComponentNode* ComponentNode;
    public AtkComponentNode* ComponentNodeAgain;
    public AtkResNode* ContainerResNode;
    public AtkTextNode* TextNode;
    public AtkNineGridNode* BubbleBackground;
    public AtkImageNode* BubbleNipple;
    [ValueParser.HexValue] public ulong Unknown6;
}

[StructLayout(LayoutKind.Sequential, Size = ByteSize)]
public struct MiniTalkBubbles {
    public const int Count = 10;
    public const int ByteSize = MiniTalkBubble.ByteSize * Count;
        
    public MiniTalkBubble Bubble0;
    public MiniTalkBubble Bubble1;
    public MiniTalkBubble Bubble2;
    public MiniTalkBubble Bubble3;
    public MiniTalkBubble Bubble4;
    public MiniTalkBubble Bubble5;
    public MiniTalkBubble Bubble6;
    public MiniTalkBubble Bubble7;
    public MiniTalkBubble Bubble8;
    public MiniTalkBubble Bubble9;

    public MiniTalkBubble this[int index] {
        get {
            return index switch {
                0 => Bubble0, 1 => Bubble1,
                2 => Bubble2, 3 => Bubble3,
                4 => Bubble4, 5 => Bubble5,
                6 => Bubble6, 7 => Bubble7,
                8 => Bubble8, 9 => Bubble9,
                _ => throw new Exception("Invalid Bubble Index")
            };
        }
    }
}