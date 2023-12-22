using Dalamud.Utility;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.STD;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Interop.Attributes;
using Dalamud.Memory;
using LuminaAddon = Lumina.Excel.GeneratedSheets.Addon;
using Addon = FFXIVClientStructs.Attributes.Addon;
using System.Text;
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

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public struct SearchContextVTable {
        [FieldOffset(0x00)] public delegate* unmanaged<SearchContext*, bool, void> Destroy;
        [FieldOffset(0x08)] public delegate* unmanaged<SearchContext*, bool> IsComplete;
        [FieldOffset(0x10)] public delegate* unmanaged<SearchContext*, void> Iterate;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xA0)]
    public struct SearchContextData {
        [FieldOffset(0x30)] public StdDeque<StdPair<ulong, ulong>> Results;
        [FieldOffset(0x70)] public nint Callback2;
        [FieldOffset(0x78)] public nint Callback;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x260)]
    public struct SearchContext {
        [FieldOffset(0x00)] public SearchContextVTable* VTable;
        [FieldOffset(0xE0)] public bool IsExact;
        [FieldOffset(0xE1)] public bool IsComplete;
        [FieldOffset(0xE2)] public bool CanIterate;
        [FieldOffset(0xE4)] public uint RowEndIdx;
        [FieldOffset(0xE8)] public uint RowStartIdx;
        [FieldOffset(0xF0)] public SearchContextData CtxData;
        [FieldOffset(0x190)] public SearchContextData CtxData2;
        [FieldOffset(0x258)] public StdVector<uint>* VectorPtr;
    }

    [Agent(AgentId.RecipeNote)]
    [StructLayout(LayoutKind.Explicit, Size = 0x568)]
    public unsafe partial struct AgentRecipeNote2 {
        [FieldOffset(0x0)] public AgentInterface AgentInterface;

        [FieldOffset(0x3BC)] public int SelectedRecipeIndex;
        [FieldOffset(0x3D4)] public uint ActiveCraftRecipeId; // 0 when not actively crafting, does not include 0x10_000
        [FieldOffset(0x3EC)] public bool RecipeSearchOpen;
        [FieldOffset(0x406)] public bool RecipeSearchProcessing;
        [FieldOffset(0x408)] public Utf8String RecipeSearch;

        [FieldOffset(0x478)] public SearchContext* SearchContext;
        [FieldOffset(0x480)] public StdVector<uint> SearchResults;

        [FieldOffset(0x498)] public byte RecipeSearchHistorySelected;
        [FieldOffset(0x4A0)] public StdDeque<Utf8String> RecipeSearchHistory;
    }

    [Agent(AgentId.ItemSearch)]
    [StructLayout(LayoutKind.Explicit, Size = 0x37F0)]
    public partial struct AgentItemSearch2
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x98)]
        public struct StringHolder
        {
            [FieldOffset(0x10)] public int Unk90Size;
            [FieldOffset(0x28)] public Utf8String SearchParam;
            [FieldOffset(0x90)] public nint Unk90Ptr;
        }

        [FieldOffset(0x0)] public AgentInterface AgentInterface;
        [FieldOffset(0x98)] public StringHolder* StringData;
        [FieldOffset(0x3304)] public uint ResultItemID;
        [FieldOffset(0x330C)] public uint ResultSelectedIndex;
        [FieldOffset(0x331C)] public uint ResultHoveredIndex;
        [FieldOffset(0x3324)] public uint ResultHoveredCount;
        [FieldOffset(0x332C)] public byte ResultHoveredHQ;
        [FieldOffset(0x37D0)] public uint* ItemBuffer;
        [FieldOffset(0x37D8)] public uint ItemCount;
        [FieldOffset(0x37E4)] public bool IsPartialSearching;
        [FieldOffset(0x37E5)] public bool IsItemPushPending;
    }

    [Addon("ItemSearch")]
    [StructLayout(LayoutKind.Explicit, Size = 0x33E0)]
    public partial struct AddonItemSearch
    {
        [FieldOffset(0x0)] public AtkUnitBase Base;
        [FieldOffset(0x190)] public bool IsPartialSearchEnabled;
        [FieldOffset(0x238)] public Utf8String String238;
        [FieldOffset(0x2A0)] public Utf8String String2A0;
        [FieldOffset(0x308)] public Utf8String String308;
        [FieldOffset(0x370)] public Utf8String String370;
        [FieldOffset(0x3D8)] public Utf8String String3D8;
        [FieldOffset(0x440)] public Utf8String String440;
        [FixedSizeArray<Utf8String>(96)]
        [FieldOffset(0x4A8)] public fixed byte StringArray[96 * 0x68]; // 96 * Utf8String
        [FieldOffset(0x3210)] public AtkComponentCheckBox* PartialSearchCheckBox;
        [FieldOffset(0x3218)] public AtkTextNode* SearchPanelTitle;
    }

    private delegate void RecipeNoteRecieveDelegate(AgentRecipeNote2* a1, Utf8String* a2, bool a3, bool a4);
    [TweakHook, Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 80 B9", DetourName = nameof(RecipeNoteRecieveDetour))]
    private readonly HookWrapper<RecipeNoteRecieveDelegate> recipeNoteRecieveHook;
    
    private delegate void RecipeNoteIterateDelegate(SearchContext* a1);
    [TweakHook, Signature("80 B9 ?? ?? ?? ?? ?? 74 27 8B 81", DetourName = nameof(RecipeNoteIterateDetour))]
    private readonly HookWrapper<RecipeNoteIterateDelegate> recipeNoteIterateHook;
    
    private delegate void AgentItemSearchUpdate1Delegate(AgentItemSearch2* a1);
    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? ?? 75 19", DetourName = nameof(AgentItemSearchUpdate1Detour))]
    private readonly HookWrapper<AgentItemSearchUpdate1Delegate> agentItemSearchUpdate1Hook;

    private delegate void AgentItemSearchUpdateAtkValuesDelegate(AgentItemSearch2* a1, uint a2, byte* a3, bool a4);
    [TweakHook, Signature("40 55 56 41 56 B8", DetourName = nameof(AgentItemSearchUpdateAtkValuesDetour))]
    private readonly HookWrapper<AgentItemSearchUpdateAtkValuesDelegate> agentItemSearchUpdateAtkValuesHook;

    private delegate void AgentItemSearchPushFoundItemsDelegate(AgentItemSearch2* a1);
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 2B F8")]
    private readonly AgentItemSearchPushFoundItemsDelegate agentItemSearchPushFoundItems;

    private delegate void ResizeVectorDelegate(nint vector, int amt);
    [Signature("E8 ?? ?? ?? ?? 48 8B 57 08 48 85 D2 74 2C")]
    private readonly ResizeVectorDelegate resizeVector;

    [AddonPostSetup("ItemSearch")]
    private void SetupItemSearch(AddonItemSearch* addon) {
        var checkbox = addon->PartialSearchCheckBox;
        var text = checkbox->AtkComponentButton.ButtonTextNode;
        text->SetText(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
    }

    private void RecipeNoteRecieveDetour(AgentRecipeNote2* a1, Utf8String* a2, bool a3, bool a4) {
        if (!a1->RecipeSearchProcessing) {
            recipeNoteRecieveHook.Original(a1, a2, a3, a4);

            RecipeSearch(a2->ToString(), &a1->SearchResults);
            a1->SearchContext->IsComplete = true;
        }
    }

    private void RecipeNoteIterateDetour(SearchContext* a1) {

    }

    private void AgentItemSearchUpdate1Detour(AgentItemSearch2* a1) {
        if (a1->IsPartialSearching && !a1->IsItemPushPending) {
            ItemSearch(a1->StringData->SearchParam.ToString(), a1);
            agentItemSearchPushFoundItems(a1);
        }
    }

    private void AgentItemSearchUpdateAtkValuesDetour(AgentItemSearch2* a1, uint a2, byte* a3, bool a4) {
        var partialString = Service.Data.GetExcelSheet<LuminaAddon>().GetRow(3136).Text.ToDalamudString().ToString();
        var isPartial = MemoryHelper.ReadStringNullTerminated((nint)a3).Equals(partialString, StringComparison.Ordinal);
        if (isPartial) {
            var newText = Encoding.UTF8.GetBytes(Config.UseFuzzySearch ? "Fuzzy Item Search" : "Fast Item Search");
            fixed (byte* t = newText)
            {
                a3 = t;
            }
        }
        agentItemSearchUpdateAtkValuesHook.Original(a1, a2, a3, a4);
    }

    private void RecipeSearch(string input, StdVector<uint>* output) {
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

        foreach (var v in query)
            PushBackVector(output, v);
    }

    private void ItemSearch(string input, AgentItemSearch2* agent) {
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
        foreach (var item in query) {
            agent->ItemBuffer[agent->ItemCount++] = item;
            if (agent->ItemCount >= 100)
                break;
        }
    }

    private void PushBackVector<T>(StdVector<T>* self, T value) where T : unmanaged {
        if (sizeof(T) != 4)
            throw new ArgumentException("Only works with 4 byte types");

        if (self->Size() == self->Capacity())
            resizeVector((nint)self, 1);

        *self->Last = value;
        self->Last++;
    }
}
