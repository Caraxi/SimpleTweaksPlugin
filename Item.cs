using Lumina.Text;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin {
    [Sheet("Item")]
    public class Item : IExcelRow {
        public struct UnkStruct59Struct {
            public byte BaseParam;
            public short BaseParamValue;
        }
        public struct UnkStruct73Struct {
            public byte BaseParamSpecial;
            public short BaseParamValueSpecial;
        }

        public SeString Singular;
        public sbyte Adjective;
        public SeString Plural;
        public sbyte PossessivePronoun;
        public sbyte StartsWithVowel;
        public sbyte Unknown5;
        public sbyte Pronoun;
        public sbyte Article;
        public SeString Description;
        public SeString Name;
        public ushort Icon;
        public LazyRow<ItemLevel> LevelItem;
        public byte Rarity;
        public byte FilterGroup;
        public uint AdditionalData;
        public LazyRow<ItemUICategory> ItemUICategory;
        public LazyRow<ItemSearchCategory> ItemSearchCategory;
        public LazyRow<EquipSlotCategory> EquipSlotCategory;
        public LazyRow<ItemSortCategory> ItemSortCategory;
        public ushort Unknown19;
        public uint StackSize;
        public bool IsUnique;
        public bool IsUntradable;
        public bool IsIndisposable;
        public bool Lot;
        public uint PriceMid;
        public uint PriceLow;
        public bool CanBeHq;
        public bool IsDyeable;
        public bool IsCrestWorthy;
        public LazyRow<ItemAction> ItemAction;
        public byte Unknown31;
        public ushort Cooldowns;
        public LazyRow<ClassJob> ClassJobRepair;
        public LazyRow<Item> ItemRepair;
        public LazyRow<Item> ItemGlamour;
        public ushort Desynth;
        public bool IsCollectable;
        public bool AlwaysCollectable;
        public ushort AetherialReduce;
        public ushort Unknown40;
        public byte LevelEquip;
        public byte Unknown41;
        public byte EquipRestriction;
        public LazyRow<ClassJobCategory> ClassJobCategory;
        public LazyRow<GrandCompany> GrandCompany;
        public LazyRow<ItemSeries> ItemSeries;
        public byte BaseParamModifier;
        public ulong ModelMain;
        public ulong ModelSub;
        public LazyRow<ClassJob> ClassJobUse;
        public byte Unknown50;
        public ushort DamagePhys;
        public ushort DamageMag;
        public ushort Delayms;
        public byte Unknown54;
        public ushort BlockRate;
        public ushort Block;
        public ushort DefensePhys;
        public ushort DefenseMag;
        public UnkStruct59Struct[] UnkStruct59;
        public LazyRow<ItemSpecialBonus> ItemSpecialBonus;
        public byte ItemSpecialBonusParam;
        public UnkStruct73Struct[] UnkStruct73;
        public byte MaterializeType;
        public byte MateriaSlotCount;
        public bool IsAdvancedMeldingPermitted;
        public bool IsPvP;
        public byte Unknown89;
        public bool IsGlamourous;

        public uint RowId { get; set; }
        public uint SubRowId { get; set; }

        public void PopulateData(RowParser parser, Lumina.Lumina lumina, Language language) {
            RowId = parser.Row;
            SubRowId = parser.SubRow;

            Singular = parser.ReadColumn<SeString>(0);
            Adjective = parser.ReadColumn<sbyte>(1);
            Plural = parser.ReadColumn<SeString>(2);
            PossessivePronoun = parser.ReadColumn<sbyte>(3);
            StartsWithVowel = parser.ReadColumn<sbyte>(4);
            Unknown5 = parser.ReadColumn<sbyte>(5);
            Pronoun = parser.ReadColumn<sbyte>(6);
            Article = parser.ReadColumn<sbyte>(7);
            Description = parser.ReadColumn<SeString>(8);
            Name = parser.ReadColumn<SeString>(9);
            Icon = parser.ReadColumn<ushort>(10);
            LevelItem = new LazyRow<ItemLevel>(lumina, parser.ReadColumn<ushort>(11), language);
            Rarity = parser.ReadColumn<byte>(12);
            FilterGroup = parser.ReadColumn<byte>(13);
            AdditionalData = parser.ReadColumn<uint>(14);
            ItemUICategory = new LazyRow<ItemUICategory>(lumina, parser.ReadColumn<byte>(15), language);
            ItemSearchCategory = new LazyRow<ItemSearchCategory>(lumina, parser.ReadColumn<byte>(16), language);
            EquipSlotCategory = new LazyRow<EquipSlotCategory>(lumina, parser.ReadColumn<byte>(17), language);
            ItemSortCategory = new LazyRow<ItemSortCategory>(lumina, parser.ReadColumn<byte>(18), language);
            Unknown19 = parser.ReadColumn<ushort>(19);
            StackSize = parser.ReadColumn<uint>(20);
            IsUnique = parser.ReadColumn<bool>(21);
            IsUntradable = parser.ReadColumn<bool>(22);
            IsIndisposable = parser.ReadColumn<bool>(23);
            Lot = parser.ReadColumn<bool>(24);
            PriceMid = parser.ReadColumn<uint>(25);
            PriceLow = parser.ReadColumn<uint>(26);
            CanBeHq = parser.ReadColumn<bool>(27);
            IsDyeable = parser.ReadColumn<bool>(28);
            IsCrestWorthy = parser.ReadColumn<bool>(29);
            ItemAction = new LazyRow<ItemAction>(lumina, parser.ReadColumn<ushort>(30), language);
            Unknown31 = parser.ReadColumn<byte>(31);
            Cooldowns = parser.ReadColumn<ushort>(32);
            ClassJobRepair = new LazyRow<ClassJob>(lumina, parser.ReadColumn<byte>(33), language);
            ItemRepair = new LazyRow<Item>(lumina, parser.ReadColumn<int>(34), language);
            ItemGlamour = new LazyRow<Item>(lumina, parser.ReadColumn<int>(35), language);
            Desynth = parser.ReadColumn<ushort>(36);
            IsCollectable = parser.ReadColumn<bool>(37);
            AlwaysCollectable = parser.ReadColumn<bool>(38);
            AetherialReduce = parser.ReadColumn<ushort>(39);
            Unknown40 = parser.ReadColumn<ushort>(40);
            LevelEquip = parser.ReadColumn<byte>(41);
            Unknown41 = parser.ReadColumn<byte>(42);
            EquipRestriction = parser.ReadColumn<byte>(43);
            ClassJobCategory = new LazyRow<ClassJobCategory>(lumina, parser.ReadColumn<byte>(44), language);
            GrandCompany = new LazyRow<GrandCompany>(lumina, parser.ReadColumn<byte>(45), language);
            ItemSeries = new LazyRow<ItemSeries>(lumina, parser.ReadColumn<byte>(46), language);
            BaseParamModifier = parser.ReadColumn<byte>(47);
            ModelMain = parser.ReadColumn<ulong>(48);
            ModelSub = parser.ReadColumn<ulong>(49);
            ClassJobUse = new LazyRow<ClassJob>(lumina, parser.ReadColumn<byte>(50), language);
            Unknown50 = parser.ReadColumn<byte>(51);
            DamagePhys = parser.ReadColumn<ushort>(52);
            DamageMag = parser.ReadColumn<ushort>(53);
            Delayms = parser.ReadColumn<ushort>(54);
            Unknown54 = parser.ReadColumn<byte>(55);
            BlockRate = parser.ReadColumn<ushort>(56);
            Block = parser.ReadColumn<ushort>(57);
            DefensePhys = parser.ReadColumn<ushort>(58);
            DefenseMag = parser.ReadColumn<ushort>(59);
            UnkStruct59 = new UnkStruct59Struct[6];
            for (var i = 0; i < 6; i++) {
                UnkStruct59[i] = new UnkStruct59Struct();
                UnkStruct59[i].BaseParam = parser.ReadColumn<byte>(60 + (i * 2 + 0));
                UnkStruct59[i].BaseParamValue = parser.ReadColumn<short>(60 + (i * 2 + 1));
            }
            ItemSpecialBonus = new LazyRow<ItemSpecialBonus>(lumina, parser.ReadColumn<byte>(72), language);
            ItemSpecialBonusParam = parser.ReadColumn<byte>(73);
            UnkStruct73 = new UnkStruct73Struct[6];
            for (var i = 0; i < 6; i++) {
                UnkStruct73[i] = new UnkStruct73Struct();
                UnkStruct73[i].BaseParamSpecial = parser.ReadColumn<byte>(74 + (i * 2 + 0));
                UnkStruct73[i].BaseParamValueSpecial = parser.ReadColumn<short>(74 + (i * 2 + 1));
            }
            MaterializeType = parser.ReadColumn<byte>(86);
            MateriaSlotCount = parser.ReadColumn<byte>(87);
            IsAdvancedMeldingPermitted = parser.ReadColumn<bool>(88);
            IsPvP = parser.ReadColumn<bool>(89);
            Unknown89 = parser.ReadColumn<byte>(90);
            IsGlamourous = parser.ReadColumn<bool>(91);
        }
    }
}