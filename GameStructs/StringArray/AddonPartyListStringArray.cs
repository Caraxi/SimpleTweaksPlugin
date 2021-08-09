using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.GameStructs.StringArray {
    
    [StructLayout(LayoutKind.Sequential, Size = 8 * 174)]
    public unsafe struct AddonPartyListStringArray {

        public byte* String000;
        public byte* String001;
        public byte* String002;
        public byte* String003;
        public byte* String004;
        public byte* String005;

        public AddonPartyListPartyMemberStrings PartyMember0;
        public AddonPartyListPartyMemberStrings PartyMember1;
        public AddonPartyListPartyMemberStrings PartyMember2;
        public AddonPartyListPartyMemberStrings PartyMember3;
        public AddonPartyListPartyMemberStrings PartyMember4;
        public AddonPartyListPartyMemberStrings PartyMember5;
        public AddonPartyListPartyMemberStrings PartyMember6;
        public AddonPartyListPartyMemberStrings PartyMember8;
        
        
        
    }
    
    [StructLayout(LayoutKind.Sequential, Size = 8 * 13)]
    public unsafe struct AddonPartyListPartyMemberStrings {
        public byte* String00;
        public byte* String01;
        public byte* String02;
        public byte* String03;
        public byte* String04;
        public byte* String05;
        public byte* String06;
        public byte* String07;
        public byte* String08;
        public byte* String09;
        public byte* String10;
        public byte* String11;
        public byte* String12;
    }
    
    
    
    
}
