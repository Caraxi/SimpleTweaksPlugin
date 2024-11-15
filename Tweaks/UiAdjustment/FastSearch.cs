using Dalamud.Utility;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.STD;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using Dalamud.Memory;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Fast Item Search")]
[TweakDescription("Enable superfast searches for the market board & crafting log.")]
[TweakAuthor("Asriel")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.2.0")]
[Changelog("1.9.2.1", "Fix random ordering (results are now always in the same order)")]
public unsafe class FastSearch : UiAdjustments.SubTweak {
    public class FastSearchConfig : TweakConfig {
        [TweakConfigOption("Use Fuzzy Search")]
        public bool UseFuzzySearch;
    }

    public FastSearchConfig Config { get; private set; }

    [Agent(AgentId.ItemSearch)]
    [StructLayout(LayoutKind.Explicit, Size = 0x37F0)]
    public partial struct AgentItemSearch2 {
        [FieldOffset(0x0000)] public AgentItemSearch AgentItemSearch;
        [FieldOffset(0x3858)] public uint* ItemBuffer;
        [FieldOffset(0x3860)] public uint ItemCount;
        [FieldOffset(0x386C)] public byte IsPartialSearching;
        [FieldOffset(0x386D)] public byte IsItemPushPending;
    }

    private delegate void RecipeNoteRecieveDelegate(AgentRecipeNote* a1, Utf8String* a2, bool a3, bool a4);

    [TweakHook, Signature("40 55 56 57 48 83 EC 20 80 B9", DetourName = nameof(RecipeNoteRecieveDetour))]
    private readonly HookWrapper<RecipeNoteRecieveDelegate> recipeNoteRecieveHook;

    [TweakHook, Signature("80 B9 ?? ?? ?? ?? ?? 74 27 8B 81 ?? ?? ?? ?? 41 B8", DetourName = nameof(RecipeNoteIterateDetour))]
    private readonly HookWrapper<System.Action> recipeNoteIterateHook;

    private delegate void AgentItemSearchUpdateDelegate(AgentItemSearch* a1);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? ?? 0F 85", DetourName = nameof(AgentItemSearchUpdateDetour))]
    private readonly HookWrapper<AgentItemSearchUpdateDelegate> agentItemSearchUpdate1Hook;

    private delegate void AgentItemSearchUpdateAtkValuesDelegate(AgentItemSearch* a1, uint a2, byte* a3, bool a4);

    [TweakHook, Signature("40 55 56 41 56 B8", DetourName = nameof(AgentItemSearchUpdateAtkValuesDetour))]
    private readonly HookWrapper<AgentItemSearchUpdateAtkValuesDelegate> agentItemSearchUpdateAtkValuesHook;

    private delegate void AgentItemSearchPushFoundItemsDelegate(AgentItemSearch* a1);

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 41 2B C9")]
    private readonly AgentItemSearchPushFoundItemsDelegate agentItemSearchPushFoundItems;

    [AddonPostSetup("ItemSearch")]
    private void SetupItemSearch(AddonItemSearch* addon) {
        var checkbox = addon->PartialSearchCheckBox;
        var text = checkbox->AtkComponentButton.ButtonTextNode;
        text->SetText(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
    }

    private void RecipeNoteRecieveDetour(AgentRecipeNote* a1, Utf8String* a2, bool a3, bool a4) {
        if (!a1->RecipeSearchProcessing) {
            recipeNoteRecieveHook.Original(a1, a2, a3, a4);

            RecipeSearch(a2->ToString(), &a1->SearchResults);
            a1->SearchContext->IsComplete = true;
        }
    }

    private void RecipeNoteIterateDetour() { }

    private void AgentItemSearchUpdateDetour(AgentItemSearch* a1) {
        if (((AgentItemSearch2*)a1)->IsPartialSearching != 0 && ((AgentItemSearch2*)a1)->IsItemPushPending == 0) {
            ItemSearch(a1->StringData->SearchParam.ToString(), a1);
            agentItemSearchPushFoundItems(a1);
        }
    }

    private void AgentItemSearchUpdateAtkValuesDetour(AgentItemSearch* a1, uint a2, byte* a3, bool a4) {
        var partialString = Service.Data.GetExcelSheet<LuminaAddon>()?.GetRow(3136)?.Text.ExtractText();
        var isPartial = MemoryHelper.ReadStringNullTerminated((nint)a3).Equals(partialString, StringComparison.Ordinal);
        if (isPartial) {
            var newText = Encoding.UTF8.GetBytes(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
            fixed (byte* t = newText) {
                a3 = t;
            }
        }

        agentItemSearchUpdateAtkValuesHook.Original(a1, a2, a3, a4);
    }

    private void RecipeSearch(string input, StdVector<uint>* output) {
        if (string.IsNullOrWhiteSpace(input))
            return;

        var sheet = Service.Data.GetExcelSheet<Recipe>();
        if (sheet == null) return;

        var validRows = sheet.Where(r => r.RecipeLevelTable.Row != 0 && r.ItemResult.Row != 0);
        var matcher = new FuzzyMatcher(input.ToLowerInvariant(), Config.UseFuzzySearch ? MatchMode.FuzzyParts : MatchMode.Simple);
        var query = validRows.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.ItemResult.Value!.Name.ToDalamudString().ToString().ToLowerInvariant()))).Where(t => t.Score > 0).OrderByDescending(t => t.Score).ThenBy(t => t.Item.RowId).Select(t => t.Item.RowId);

        output->AddRangeCopy(query);
    }

    private void ItemSearch(string input, AgentItemSearch* agent) {
        if (string.IsNullOrWhiteSpace(input))
            return;
        var sheet = Service.Data.GetExcelSheet<Item>();
        if (sheet == null) return;
        var marketItems = sheet.Where(i => i.ItemSearchCategory.Row != 0);
        var matcher = new FuzzyMatcher(input.ToLowerInvariant(), Config.UseFuzzySearch ? MatchMode.FuzzyParts : MatchMode.Simple);
        var query = marketItems.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.Name.ToDalamudString().ToString().ToLowerInvariant()))).Where(t => t.Score > 0).OrderByDescending(t => t.Score).ThenBy(t => t.Item.RowId).Select(t => t.Item.RowId);
        foreach (var item in query) {
            var a = (AgentItemSearch2*)agent;
            a->ItemBuffer[a->ItemCount++] = item;
            if (a->ItemCount >= 100)
                break;
        }
    }
}
