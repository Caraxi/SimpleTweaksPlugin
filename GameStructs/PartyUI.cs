using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.GameStructs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x100)]
    public unsafe struct SortedMember
    {
        [FieldOffset(0x00)] private AtkComponentBase* buff0; //*10
        [FieldOffset(0x08)] private AtkComponentBase* buff1;
        [FieldOffset(0x10)] private AtkComponentBase* buff2;
        [FieldOffset(0x18)] private AtkComponentBase* buff3;
        [FieldOffset(0x20)] private AtkComponentBase* buff4;
        [FieldOffset(0x28)] private AtkComponentBase* buff5;
        [FieldOffset(0x30)] private AtkComponentBase* buff6;
        [FieldOffset(0x38)] private AtkComponentBase* buff7;
        [FieldOffset(0x40)] private AtkComponentBase* buff8;
        [FieldOffset(0x48)] private AtkComponentBase* buff9;
        [FieldOffset(0x50)] public AtkComponentNode* Component;
        [FieldOffset(0x58)] public AtkTextNode* enmityTextNode; // Sorted[11] =2   Icon左下Text（仇恨排名什么的）
        [FieldOffset(0x60)] public AtkResNode* nameResNode; // Sorted[12] =14  姓名版
        [FieldOffset(0x68)] public AtkTextNode* partyNumberTextNode; // Sorted[13] =15  姓名版排序号
        [FieldOffset(0x70)] public AtkTextNode* nameTextNode; //Sorted[14] =16  角色名
        [FieldOffset(0x78)] public AtkTextNode* spellTextNode; //Sorted[15] =4   动作名
        [FieldOffset(0x80)] public AtkImageNode* castBarProgressImageNode; //Sorted[16] =5   施法条进度
        [FieldOffset(0x88)] public AtkImageNode* castBarEdgeImageNode; //Sorted[17] =6   施法条外框
        [FieldOffset(0x90)] public AtkResNode* enmityResNode; //Sorted[18] =7   仇恨条
        [FieldOffset(0x98)] public AtkNineGridNode* enmityBarNineGridNode; //Sorted[19] =8   仇恨量
        [FieldOffset(0xA0)] public AtkImageNode* iconNode; //Sorted[20] =11  职业Icon
        [FieldOffset(0xA8)] public AtkUldPart* iconAtkUldPart; //Sorted[21] =11? 职业Icon.AtkTexture
        [FieldOffset(0xB0)] public AtkImageNode* UnknownNode; //Sorted[22] =10  跨服Icon              亲信=0
        [FieldOffset(0xB8)] public AtkUldPart* UnknownAtkUldPart; //Sorted[23] =10? 跨服职业Icon.AtkTexture   亲信=0
        [FieldOffset(0xC0)] public AtkComponentBase* hpComponentBase; //Sorted[24] =    HP部分 Components
        [FieldOffset(0xC8)] public AtkComponentBase* hpBarComponentBase; //Sorted[25] =    HP条 Components
        [FieldOffset(0xD0)] public AtkComponentBase* mpBarComponentBase; //Sorted[26] =    MP部分 Components     宠物=0
        [FieldOffset(0xD8)] public AtkResNode* selectedResNode; //Sorted[27] =27  选中ResNode
        [FieldOffset(0xE0)] public AtkNineGridNode* halfLockNineGridNode; //Sorted[28] =29  半锁定bg
        [FieldOffset(0xE8)] public AtkNineGridNode* lockNineGridNode; //Sorted[29] =30  锁定bg
        [FieldOffset(0xF0)] public AtkCollisionNode* collisionNode; //Sorted[30] =31  碰撞Node
        [FieldOffset(0xF8)] public byte enmityNumber; //仇恨（1仇==1，2仇==2）其余==FF

        public AtkComponentBase* Buff(int index)
        {     
            return index switch
            {
                0 => buff0,
                1 => buff1,
                2 => buff2,
                3 => buff3,
                4 => buff4,
                5 => buff5,
                6 => buff6,
                7 => buff7,
                8 => buff8,
                9 => buff9,
                _ => null
            };
        }

    }


    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct PartyUi
    {
        //[FieldOffset(0x0)] public AtkEventListener AtkEventListener;
        //[FieldOffset(0x8)] public fixed byte Name[0x20];

        //[FieldOffset(0x028)] public ULDData ULDData;
        //[FieldOffset(0x028)] public AtkUldManager UldManager;

        //[FieldOffset(0x0C8)] public AtkResNode* RootNode;
        ////[FieldOffset(0x0D8)] FFFF or FFFB;

        //[FieldOffset(0x0F0)] public AtkCollisionNode* CollisionNode;
        //[FieldOffset(0x108)] public AtkComponentNode* WindowNode;

        ////[FieldOffset(0x19C)] FFFFFFFF or FDFFFFFF;
        //[FieldOffset(0x1AC)] public float Scale;
        //[FieldOffset(0x182)] public byte Flags;
        //[FieldOffset(0x1BC)] public short X;
        //[FieldOffset(0x1BE)] public short Y;

        //[FieldOffset(0x1CC)] public short ID;
        //[FieldOffset(0x1CE)] public short ParentID;
        //[FieldOffset(0x1D5)] public byte Alpha;

        //[FieldOffset(0x1D8)] public AtkCollisionNode** CollisionNodeList; 
        //// seems to be all collision nodes in tree, may be something else though

        //[FieldOffset(0x1E0)] public uint CollisionNodeListCount;

        ////[FieldOffset(0x1F6)] Unknown Textnode*;

        [FieldOffset(0x220)] private SortedMember Member0; //*13;
        [FieldOffset(0x320)] private SortedMember Member1;
        [FieldOffset(0x420)] private SortedMember Member2;
        [FieldOffset(0x520)] private SortedMember Member3;
        [FieldOffset(0x620)] private SortedMember Member4;
        [FieldOffset(0x720)] private SortedMember Member5;
        [FieldOffset(0x820)] private SortedMember Member6;
        [FieldOffset(0x920)] private SortedMember Member7;
        [FieldOffset(0xA20)] private SortedMember Member8;
        [FieldOffset(0xB20)] private SortedMember Member9;
        [FieldOffset(0xC20)] private SortedMember Member10;
        [FieldOffset(0xD20)] private SortedMember Member11;
        [FieldOffset(0xE20)] private SortedMember Member12;

        [FieldOffset(0xF20)] public fixed int JobId[13]; //ClassJob+F294 or 0
        [FieldOffset(0xF54)] public fixed int UnknownINT[13];

        [FieldOffset(0xF88)] public fixed short Edited[13]; //0X11 if edited? Need comfirm

        [FieldOffset(0xFA8)] public AtkNineGridNode* BackgroundNineGridNode; //= Background;
        [FieldOffset(0xFB0)] public AtkTextNode* SoloTextNode; //= Solo指示;
        [FieldOffset(0xFB8)] public AtkResNode* LeaderResNode; //= 队长指示(Res);
        [FieldOffset(0xFC0)] public AtkResNode* MpBarSpecialResNode; //= 蓝条特殊Res;
        [FieldOffset(0xFC8)] public AtkTextNode* MpBarSpecialTextNode; //= 蓝条特殊Text;

        [FieldOffset(0xFD0)] public short LocalCount; //本地
        [FieldOffset(0xFD4)] public short CrossRealmCount; //跨服
        [FieldOffset(0xFD8)] public short LeaderNumber; //or FFFF // (从0开始计数)

        [FieldOffset(0xFDC)] public short HideWhenSolo;
        //[FieldOffset(0xFE0)] FFFFFFFF
        //[FieldOffset(0xFE4)]FFFFFFFF
        //[FieldOffset(0xFE8)] = 蓝条特殊Res.Y;
        //[FieldOffset(0xFEC)] &= FFFE

        [FieldOffset(0xFF1)] public byte PetCount;
        [FieldOffset(0xFF2)] public byte CPCount;


        public SortedMember Member(int index)
        {
            return index switch
            {
                0 => Member0,
                1 => Member1,
                2 => Member2,
                3 => Member3,
                4 => Member4,
                5 => Member5,
                6 => Member6,
                7 => Member7,
                8 => Member8,
                9 => Member9,
                10 => Member10,
                11 => Member11,
                12 => Member12,
                _ => new SortedMember(),
            };
        }


    }
}