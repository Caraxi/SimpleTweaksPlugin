// ReSharper disable All

using System;
using Lumina;
using Lumina.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Sheets; 

[Sheet( "Item" )]
public class ExtendedItem : Item
{

    public struct BaseParamStruct {
        public LazyRow<ExtendedBaseParam> BaseParam;
        public short Value;
    }

    public byte LevelSyncFlag { get; set; }
    public BaseParamStruct[] BaseParam { get; set; }
    public BaseParamStruct[] BaseParamSpecial { get; set; }
        
        
    public override void PopulateData( RowParser parser, GameData gameData, Language language )
    {
        base.PopulateData( parser, gameData, language );
            
        LevelSyncFlag = parser.ReadColumn< byte >( 89 );
            
        BaseParam = new BaseParamStruct[ 6 ];
        for( var i = 0; i < 6; i++ )
        {
            BaseParam[ i ] = new BaseParamStruct();
            BaseParam[ i ].BaseParam = new LazyRow<ExtendedBaseParam>(gameData, parser.ReadColumn< byte >( 59 + ( i * 2 + 0 ) ), language);
            BaseParam[ i ].Value = parser.ReadColumn< short >( 59 + ( i * 2 + 1 ) );
        }

        BaseParamSpecial = new BaseParamStruct[ 6 ];
        for( var i = 0; i < 6; i++ )
        {
            BaseParamSpecial[ i ] = new BaseParamStruct();
            BaseParamSpecial[ i ].BaseParam = new LazyRow<ExtendedBaseParam>(gameData, parser.ReadColumn< byte >( 73 + ( i * 2 + 0 ) ), language);
            BaseParamSpecial[ i ].Value = parser.ReadColumn< short >( 73 + ( i * 2 + 1 ) );
        }

    }
}