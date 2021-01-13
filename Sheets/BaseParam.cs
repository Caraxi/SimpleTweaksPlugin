using Lumina.Text;
using Lumina.Excel;

namespace SimpleTweaksPlugin.Sheets {
    [Sheet( "BaseParam")]
    public class BaseParam : CustomSheet {
        public readonly byte[] EquipSlotCategoryPct = new byte[22];
        public SeString Name;
        
        public override void Populate(RowParser parser) {
            base.Populate(parser);
            Name = parser.ReadColumn<SeString>(1);
            for (var i = 1; i < EquipSlotCategoryPct.Length; i++) {
                EquipSlotCategoryPct[i] = parser.ReadColumn<byte>(i + 3);
            }
        }
    }
}