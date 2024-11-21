// ReSharper disable All

using Lumina;
using Lumina.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Sheets; 

[Sheet( "RecipeLookup")]
readonly unsafe public struct ExtendedRecipeLookup(ExcelPage page, uint offset, uint row) : IRowExtension<ExtendedRecipeLookup, RecipeLookup> {
    public readonly RecipeLookup RecipeLookup => new RecipeLookup(page, offset, row);
    
    public readonly RowRef<Recipe>[] Recipes =>
        new RowRef<Recipe>[] {
            RecipeLookup.CRP, RecipeLookup.BSM,
            RecipeLookup.ARM, RecipeLookup.GSM,
            RecipeLookup.LTW, RecipeLookup.WVR,
            RecipeLookup.ALC, RecipeLookup.CUL
        };
    
    public static ExtendedRecipeLookup Create(ExcelPage page, uint offset, uint row) => new(page, offset, row);
    public uint RowId => row; 
}

