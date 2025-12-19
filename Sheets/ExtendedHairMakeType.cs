using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Sheets;

[Sheet("HairMakeType")]
public readonly unsafe struct HairMakeTypeExt : IRowExtension<HairMakeTypeExt, HairMakeType> {
    public uint RowId { get; }
    public ExcelPage ExcelPage { get; }
    public uint RowOffset { get; }
    public RowRef<CharaMakeCustomize>[] HairStyles { get; }
    public HairMakeType HairMakeType { get; }

    public RowRef<Tribe> Tribe => HairMakeType.Tribe;
    public sbyte Gender => HairMakeType.Gender;
    
    public HairMakeTypeExt(ExcelPage page, uint offset, uint row) {
        RowId = row;
        ExcelPage = page;
        RowOffset = offset;
        HairMakeType = new HairMakeType(page, offset, row);
        
        var hairstyles = new List<uint>();
        for (var i = 0U; i < 100; i++) {
            var id = page.ReadUInt32(offset + 0xC + 4 * i);
            if (id == 0) break;
            hairstyles.Add(id);
        }

        HairStyles = hairstyles.Select(h => new RowRef<CharaMakeCustomize>(page.Module, h, page.Language)).ToArray();
    }
    
    public static HairMakeTypeExt Create(ExcelPage page, uint offset, uint row) => new(page, offset, row);
}
