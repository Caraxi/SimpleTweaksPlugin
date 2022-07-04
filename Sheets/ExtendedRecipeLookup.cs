// ReSharper disable All

using Lumina.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel.GeneratedSheets
{
    [Sheet( "RecipeLookup")]
    public class ExtendedRecipeLookup : RecipeLookup {

        public LazyRow<Recipe>[] Recipes { get; private set; }

        public override void PopulateData( RowParser parser, GameData gameData, Language language )
        {
            base.PopulateData( parser, gameData, language );
            Recipes = new LazyRow<Recipe>[] { CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL };
        }
    }
}
