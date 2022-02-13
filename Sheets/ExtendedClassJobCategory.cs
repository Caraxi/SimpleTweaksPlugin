using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Sheets; 

public class ExtendedClassJobCategory : ClassJobCategory {

    private bool[] classJob;

    public bool this[ ClassJob cj ] => cj.RowId < classJob.Length && classJob[ cj.RowId ];
    public bool this[ uint cj ] => cj < classJob.Length && classJob[ cj ];

    public override void PopulateData( RowParser parser, GameData gameData, Language language ) {
        base.PopulateData( parser, gameData, language );

        classJob = new bool[parser.Sheet.ColumnCount - 1];
        for( var i = 0; i < parser.Sheet.ColumnCount - 1; i++ ) {
            classJob[ i ] = parser.ReadColumn< bool >( i + 1 );
        }
    }
}