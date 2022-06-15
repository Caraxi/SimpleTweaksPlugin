﻿using Lumina;
using Lumina.Data;
using Lumina.Excel;

namespace SimpleTweaksPlugin.Sheets; 

[Sheet( "BaseParam")]
public class ExtendedBaseParam : Lumina.Excel.GeneratedSheets.BaseParam {
    public readonly ushort[] EquipSlotCategoryPct = new ushort[22];

    public override void PopulateData(RowParser parser, GameData gameData, Language language) {
        base.PopulateData(parser, gameData, language);
        for (var i = 1; i < EquipSlotCategoryPct.Length; i++) {
            EquipSlotCategoryPct[i] = parser.ReadColumn<ushort>(i + 3);
        }
    }
}