using System.Linq;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public unsafe class OpenCraftingRecipe : ItemHotkey {
    protected override string Name => "Open Crafting Recipe";
    protected override VirtualKey[] DefaultKeyCombo => [VirtualKey.CONTROL, VirtualKey.R];

    public override void OnTriggered(Item item) {
        var recipes = Service.Data.Excel.GetSheet<Recipe>()?.Where(r => r.ItemResult.RowId == item.RowId).ToList();

        if (recipes?.Count == 1) {
            AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipes[0].RowId);
        } else {
            AgentRecipeNote.Instance()->OpenRecipeByItemId(item.RowId);
        }
    }

    private bool doShowCache;
    private uint doShowCacheId;

    public override bool DoShow(Item item) {
        if (doShowCacheId == item.RowId) return doShowCache;
        doShowCacheId = item.RowId;
        doShowCache = Service.Data.Excel.GetSheet<Recipe>()?.Any(r => r.ItemResult.RowId == item.RowId) ?? false;
        return doShowCache;
    }
}
