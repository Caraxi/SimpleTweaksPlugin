using Lumina.Excel;

namespace SimpleTweaksPlugin.Sheets {
    [Sheet( "ItemLevel")]
    public class ItemLevel : CustomSheet {
        public readonly ushort[] BaseParam = new ushort[75];

        public override void Populate(RowParser parser) {
            base.Populate(parser);
            for (var i = 1; i < BaseParam.Length; i++) {
                BaseParam[i] = parser.ReadColumn<ushort>(i - 1);
            }
        }
    }
}