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

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Fast Recipe Search")]
[TweakDescription("Enable superfast searches for recipes in the crafting log.")]
[TweakAuthor("Asriel")]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
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

    private delegate void RecipeNoteRecieveDelegate(AgentRecipeNote2* a1, Utf8String* a2, bool a3, bool a4);
    private HookWrapper<RecipeNoteRecieveDelegate> recipeNoteRecieveHook;

    private delegate void RecipeNoteIterateDelegate(SearchContext* a1);
    private HookWrapper<RecipeNoteIterateDelegate> recipeNoteIterateHook;

    private delegate void ResizeVectorDelegate(nint vector, int amt);
    private ResizeVectorDelegate resizeVector;

    public override void Setup() {
        AddChangelog("1.9.2", "New Tweak");
        base.Setup();
    }

    protected override void Enable() {
        recipeNoteRecieveHook ??= Common.Hook<RecipeNoteRecieveDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 80 B9", RecipeNoteRecieveDetour);
        recipeNoteRecieveHook?.Enable();

        recipeNoteIterateHook ??= Common.Hook<RecipeNoteIterateDelegate>("80 B9 ?? ?? ?? ?? ?? 74 27 8B 81", RecipeNoteIterateDetour);
        recipeNoteIterateHook?.Enable();

        if (resizeVector is null) {
            var resizeVectorPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 57 08 48 85 D2 74 2C");
            resizeVector = Marshal.GetDelegateForFunctionPointer<ResizeVectorDelegate>(resizeVectorPtr);
        }

        Config = LoadConfig<FastSearchConfig>() ?? new();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        recipeNoteRecieveHook?.Disable();
        recipeNoteIterateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        recipeNoteRecieveHook?.Dispose();
        recipeNoteIterateHook?.Dispose();
        base.Dispose();
    }

    private void RecipeNoteRecieveDetour(AgentRecipeNote2* a1, Utf8String* a2, bool a3, bool a4) {
        if (!a1->RecipeSearchProcessing)
        {
            recipeNoteRecieveHook.Original(a1, a2, a3, a4);

            Search(a2->ToString(), &a1->SearchResults);
            a1->SearchContext->IsComplete = true;
        }
    }

    private void RecipeNoteIterateDetour(SearchContext* a1) {

    }

    private void Search(string input, StdVector<uint>* output) {
        var sheet = Service.Data.GetExcelSheet<Recipe>().Where(r => r.RecipeLevelTable.Row != 0 && r.ItemResult.Row != 0);
        if (string.IsNullOrWhiteSpace(input))
            return;
        var matcher = new FuzzyMatcher(input.ToLowerInvariant(), Config.UseFuzzySearch ? MatchMode.FuzzyParts : MatchMode.Simple);
        var query = sheet.AsParallel()
            .Select(i => (Item: i, Score: matcher.Matches(i.ItemResult.Value!.Name.ToDalamudString().ToString().ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Item.RowId);

        foreach (var v in query)
            PushBackVector(output, v);
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
