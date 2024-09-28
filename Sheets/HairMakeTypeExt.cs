using System.Collections.Generic;
using System.Linq;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;

namespace SimpleTweaksPlugin.Sheets;

[Sheet("HairMakeType")]
public class HairMakeTypeExt : HairMakeType {
    public LazyRow<CharaMakeCustomize>[] HairStyles { get; private set; } = [];

    public override void PopulateData(RowParser parser, GameData gameData, Language language) {
        base.PopulateData(parser, gameData, language);

        var hairstyles = new List<uint>();
        for (var i = 0; i < 100; i++) {
            var id = parser.ReadOffset<uint>(0xC + 4 * i);
            if (id == 0) break;
            hairstyles.Add(id);
        }

        HairStyles = hairstyles.Select(h => new LazyRow<CharaMakeCustomize>(gameData, h, language)).ToArray();
    }
}
