using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.STD;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Linq;
using System.Text;
using LuminaAddon = Lumina.Excel.GeneratedSheets.Addon;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Fast Item Search")]
[TweakDescription("Enable superfast searches for the market board & crafting log.")]
[TweakAuthor("Asriel")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.2.0")]
[Changelog("1.9.2.1", "Fix random ordering (results are now always in the same order)")]
public unsafe class FastSearch : UiAdjustments.SubTweak
{
    public class FastSearchConfig : TweakConfig
    {
        [TweakConfigOption("Use Fuzzy Search")]
        public bool UseFuzzySearch;
    }

    public FastSearchConfig Config { get; private set; }

    [TweakHook]
    private HookWrapper<AgentRecipeNote.Delegates.SearchRecipe>? recipeNoteSearchRecipeHook;

    [TweakHook]
    private HookWrapper<RecipeSearchContext.Delegates.Iterate>? recipeNoteIterateHook;

    private delegate void AgentItemSearchUpdate1Delegate(AgentItemSearch* a1);
    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? ?? 0F 85", DetourName = nameof(AgentItemSearchUpdate1Detour))]
    private readonly HookWrapper<AgentItemSearchUpdate1Delegate> agentItemSearchUpdate1Hook;

    private delegate void AgentItemSearchUpdateAtkValuesDelegate(AgentItemSearch* a1, uint a2, byte* a3, bool a4);
    [TweakHook, Signature("E9 ?? ?? ?? ?? 45 E0 32", DetourName = nameof(AgentItemSearchUpdateAtkValuesDetour))]
    private readonly HookWrapper<AgentItemSearchUpdateAtkValuesDelegate> agentItemSearchUpdateAtkValuesHook;

    private delegate void AgentItemSearchPushFoundItemsDelegate(AgentItemSearch* a1);
    [TweakHook, Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 41 2B C9")]
    private readonly AgentItemSearchPushFoundItemsDelegate agentItemSearchPushFoundItems;

    protected override void Enable()
    {
        recipeNoteSearchRecipeHook ??= Common.Hook<AgentRecipeNote.Delegates.SearchRecipe>(AgentRecipeNote.Addresses.SearchRecipe.Value, RecipeNoteRecieveDetour);
        recipeNoteIterateHook ??= Common.Hook<RecipeSearchContext.Delegates.Iterate>(RecipeSearchContext.StaticVirtualTablePointer->Iterate, RecipeNoteIterateDetour);
        base.Enable();
    }

    [AddonPostSetup("ItemSearch")]
    private void SetupItemSearch(AddonItemSearch* addon)
    {
        var checkbox = addon->PartialSearchCheckBox;
        var text = checkbox->AtkComponentButton.ButtonTextNode;
        text->SetText(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
    }

    private void RecipeNoteRecieveDetour(AgentRecipeNote* a1, Utf8String* a2, byte a3, bool a4)
    {
        
        if (!a1->RecipeSearchProcessing)
        {
            recipeNoteSearchRecipeHook.Original(a1, a2, a3, a4);

            RecipeSearch(a2->ToString(), &a1->SearchResults);
            a1->SearchContext->IsComplete = true;
        }
    }

    private void RecipeNoteIterateDetour(RecipeSearchContext* a1)
    {

    }

    private void AgentItemSearchUpdate1Detour(AgentItemSearch* a1)
    {
        if (a1->IsPartialSearching && !a1->IsItemPushPending)
        {
            ItemSearch(a1->StringData->SearchParam.ToString(), a1);
            agentItemSearchPushFoundItems(a1);
        }
    }

    private void AgentItemSearchUpdateAtkValuesDetour(AgentItemSearch* a1, uint a2, byte* a3, bool a4)
    {
        var partialString = Service.Data.GetExcelSheet<LuminaAddon>().GetRow(3136).Text.ToDalamudString().ToString();
        var isPartial = MemoryHelper.ReadStringNullTerminated((nint)a3).Equals(partialString, StringComparison.Ordinal);
        if (isPartial)
        {
            var newText = Encoding.UTF8.GetBytes(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
            fixed (byte* t = newText)
            {
                a3 = t;
            }
        }
        agentItemSearchUpdateAtkValuesHook.Original(a1, a2, a3, a4);
    }

    private void RecipeSearch(string input, StdVector<uint>* output)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;
        var sheet = Service.Data.GetExcelSheet<Recipe>().Where(r => r.RecipeLevelTable.Row != 0 && r.ItemResult.Row != 0);
        var matcher = new FuzzyMatcher(input.ToLowerInvariant(), Config.UseFuzzySearch ? MatchMode.FuzzyParts : MatchMode.Simple);
        var query = sheet.AsParallel()
            .Select(i => (Item: i, Score: matcher.Matches(i.ItemResult.Value!.Name.ToDalamudString().ToString().ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Item.RowId)
            .Select(t => t.Item.RowId);

        output->AddRangeCopy(query);
    }

    private void ItemSearch(string input, AgentItemSearch* agent)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;
        var sheet = Service.Data.GetExcelSheet<Item>().Where(i => i.ItemSearchCategory.Row != 0);
        var matcher = new FuzzyMatcher(input.ToLowerInvariant(), Config.UseFuzzySearch ? MatchMode.FuzzyParts : MatchMode.Simple);
        var query = sheet.AsParallel()
            .Select(i => (Item: i, Score: matcher.Matches(i.Name.ToDalamudString().ToString().ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Item.RowId)
            .Select(t => t.Item.RowId);
        foreach (var item in query)
        {
            agent->ItemBuffer[agent->ItemCount++] = item;
            if (agent->ItemCount >= 100)
                break;
        }
    }
}
