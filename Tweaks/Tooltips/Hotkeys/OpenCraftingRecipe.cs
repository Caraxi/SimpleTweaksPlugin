using System.Linq;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public unsafe class OpenCraftingRecipe : ItemHotkey {
    public override string Name => "Open Crafting Recipe";
    protected override VirtualKey[] DefaultKeyCombo => new[] { VirtualKey.CONTROL, VirtualKey.R};

    public override void OnTriggered(ExtendedItem item) {
        var recipes = Service.Data.Excel.GetSheet<Recipe>()?.Where(r => r.ItemResult.Row == item.RowId).ToList();

        if (recipes?.Count == 1) {
            AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipes[0].RowId);
        } else {
            AgentRecipeNote.Instance()->OpenRecipeByItemId(item.RowId);
        }
    }

    public override bool DoShow(ExtendedItem item) {
        return Service.Data.Excel.GetSheet<Recipe>()?.Any(r => r.ItemResult.Row == item.RowId) ?? false;
    }
}

