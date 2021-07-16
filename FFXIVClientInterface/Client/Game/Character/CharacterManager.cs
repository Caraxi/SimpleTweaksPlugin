using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Internal;
using FFXIVClientInterface.Misc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace FFXIVClientInterface.Client.Game.Character {
    public unsafe class CharacterManager : StructWrapper<CharacterManagerStruct> {
        public static implicit operator CharacterManagerStruct*(CharacterManager module) => module.Data;
        public static explicit operator ulong(CharacterManager module) => (ulong) module.Data;
        
        public CharacterManager(CharacterManagerAddressResolver addressResolver) {
            lookupBattleCharaByObjectId = Marshal.GetDelegateForFunctionPointer<LookupBattleCharaByObjectIDDelegate>(addressResolver.LookupBattleCharaByObjectID);
        }

        private delegate BattleChara* LookupBattleCharaByObjectIDDelegate(CharacterManagerStruct* @this, int objectId);
        private readonly LookupBattleCharaByObjectIDDelegate lookupBattleCharaByObjectId;
        public BattleChara* LookupBattleCharaByObjectID(int objectId) => lookupBattleCharaByObjectId(Data, objectId);

        private PointerList<BattleChara> battleCharacterPointerList;
        public PointerList<BattleChara> BattleCharacter => battleCharacterPointerList ??= new PointerList<BattleChara>(Data->Character);
    }

    public class CharacterManagerAddressResolver : BaseAddressResolver {
        public IntPtr BaseAddress;

        public IntPtr LookupBattleCharaByObjectID;

        protected override void Setup64Bit(SigScanner sig) {
            BaseAddress = sig.GetStaticAddressFromSig("8B D0 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 3A");
            LookupBattleCharaByObjectID = sig.ScanText("E8 ?? ?? ?? ?? 4C 8B F0 48 85 C0 0F 84 ?? ?? ?? ?? 0F 28 05");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x338)]
    public unsafe struct CharacterManagerStruct {
        [FieldOffset(0x000)] public fixed ulong Character[100];
        [FieldOffset(0x330)] public int unknown330;
    }

}

