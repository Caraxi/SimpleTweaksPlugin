using Lumina;
using Lumina.Data;
using Lumina.Text;
using Lumina.Excel;

namespace SimpleTweaksPlugin.Sheets {
    [Sheet( "BaseParam")]
    public class ExtendedBaseParam : Lumina.Excel.GeneratedSheets.BaseParam {
        public readonly byte[] EquipSlotCategoryPct = new byte[22];
        public SeString Name;
        
        public override void PopulateData(RowParser parser, GameData gameData, Language language) {
            base.PopulateData(parser, gameData, language);
            Name = parser.ReadColumn<SeString>(1);
            for (var i = 1; i < EquipSlotCategoryPct.Length; i++) {
                EquipSlotCategoryPct[i] = parser.ReadColumn<byte>(i + 3);
            }
        }
    }
}