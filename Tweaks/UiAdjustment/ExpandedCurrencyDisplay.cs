using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Expanded Currency Display")]
[TweakDescription("Allows you to display extra currencies.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.8.3.1", "Use configured format culture for number display, should fix French issue")]
[Changelog("1.8.4.0", "Added support for Collectibles")]
[Changelog("1.8.8.0", "Added option for adjustable spacing in horizontal layouts.")]
[Changelog("1.8.8.0", "Added option to display in a grid.")]
[Changelog("1.8.8.0", "Added option to set the position of a currency individually.")]
[Changelog("1.8.8.0", "Added tooltips when mouse is over the currency icons.")]
[Changelog("1.8.8.1", "Attempting to avoid gil addon getting thrown around when layout changes.")]
[Changelog("1.8.8.2", "Fixed positioning of gil display moving when scale is anything other than 100%")]
[Changelog("1.9.0.0", "Added an option to disable tooltips.")]
[Changelog("1.9.0.0", "Fixed currency window positioning breaking when resizing game window.")]
[Changelog("1.10.8.1", "Added option to Grand Company seals to display current grand company.")]
public unsafe class ExpandedCurrencyDisplay : UiAdjustments.SubTweak {
    public class Config : TweakConfig {
        public Direction DisplayDirection = Direction.Up;
        public List<CurrencyEntry> Currencies = [];
        public bool Grid;
        public int GridSize = 2;
        public float[] GridSpacing = [];
        public Direction GridGrowth = Direction.Left;
        public bool DisableEvents;
    }
    
    public record CurrencyEntry {
        public bool Enabled = true;
        public uint ItemId;
        public int IconId;
        public bool HqItem;
        public bool CollectibleItem;
        public string Name = string.Empty;
        public float HorizontalSpacing;
        public bool UseCustomPosition;
        public Vector2 CustomPosition;
        public bool AutoAdjustGrandCompany;

        public CurrencyEntry GetAdjusted() {
            if (AutoAdjustGrandCompany && ItemId is 20 or 21 or 22) {
                var gc = PlayerState.Instance()->GrandCompany;
                if (gc is 0 or > 3) gc = PlayerState.Instance()->StartTown;
                if (gc is 0 or > 3) return this;
                return this with {
                    ItemId = 19U + gc,
                    IconId = 65003 + gc,
                    Name = Service.Data.GetExcelSheet<Item>().GetRow(19U + gc).Name.ExtractText()
                };
            }

            return this;
        }
    }

    public enum Direction {
        Left,
        Right,
        Up,
        Down,
    }

    [TweakConfig] private Config TweakConfig { get; set; } = null!;
    
    protected void DrawConfig() {
        DrawDirectionComboBox();
        
        DrawGridConfig();

        ImGuiHelpers.ScaledDummy(5.0f);
        DrawAddCurrency();
        
        ImGuiHelpers.ScaledDummy(5.0f);
        DrawCurrencies();
    }
    
    private static AtkUnitBase* AddonMoney => Common.GetUnitBase("_Money");

    private const uint ImageBaseId = 1000U;
    private const uint CounterBaseId = 2000U;
    private string searchString = string.Empty;
    private List<Item> searchedItems = [];
    private bool hqItemSearch;
    private bool collectibleItemSearch;
    private readonly Dictionary<uint, string> tooltipStrings = [];
    private SimpleEvent? simpleEvent;

    private HookWrapper<AgentHUDLayout.Delegates.Show>? openHudLayoutHook;
    private HookWrapper<AgentHUDLayout.Delegates.Hide>? closeHudLayoutHook;
    private HookWrapper<AddonConfig.Delegates.ChangeHudLayout>? hudLayoutChangeHook;
    private delegate void* UnitBaseUpdatePosition(AtkUnitBase* unitBase);
    private HookWrapper<UnitBaseUpdatePosition>? updatePositionHook;
    

    protected override void Enable() {
        simpleEvent ??= new SimpleEvent(HandleEvent);
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        if (TweakConfig.DisplayDirection is Direction.Up or Direction.Down && TweakConfig.GridGrowth is Direction.Up or Direction.Down) TweakConfig.GridGrowth = Direction.Left;
        if (TweakConfig.DisplayDirection is Direction.Left or Direction.Right && TweakConfig.GridGrowth is Direction.Left or Direction.Right) TweakConfig.GridGrowth = Direction.Up;

        openHudLayoutHook ??= Common.Hook<AgentHUDLayout.Delegates.Show>(AgentHUDLayout.Instance()->VirtualTable->Show, OnOpenHudLayout);
        openHudLayoutHook?.Enable();
        
        closeHudLayoutHook ??= Common.Hook<AgentHUDLayout.Delegates.Hide>(AgentHUDLayout.Instance()->VirtualTable->Hide, OnCloseHudLayout);
        closeHudLayoutHook?.Enable();

        hudLayoutChangeHook ??= Common.Hook<AddonConfig.Delegates.ChangeHudLayout>(AddonConfig.MemberFunctionPointers.ChangeHudLayout, OnHudLayoutChange);
        hudLayoutChangeHook?.Enable();

        updatePositionHook ??= Common.Hook<UnitBaseUpdatePosition>("E8 ?? ?? ?? ?? 48 8B 03 41 B9 ?? ?? ?? ?? 45 33 C0 41 0F B6 D1 48 8B CB FF 50 30 48 8B CB", UpdatePositionDetour);
        updatePositionHook?.Enable();

        var agent = AgentHUDLayout.Instance();
        if (!agent->IsAgentActive()) {
            Common.FrameworkUpdate += OnFrameworkUpdate;
        }
        
        base.Enable();
    }

    private void* UpdatePositionDetour(AtkUnitBase* unitBase) {
        try {
            if (unitBase->Name[0] == '_') {
                var name = unitBase->NameString;
                if (name == "_Money") FreeAllNodes();
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        

        return updatePositionHook!.Original(unitBase);
    }

    private void OnOpenHudLayout(AgentHUDLayout* thisPtr) {
        FreeAllNodes();
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        
        openHudLayoutHook!.Original(thisPtr);
    }

    private void OnCloseHudLayout(AgentHUDLayout* thisPtr) {
        FreeAllNodes();
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        Common.FrameworkUpdate += OnFrameworkUpdate;
        
        closeHudLayoutHook!.Original(thisPtr);
    }
    
    private void OnHudLayoutChange(AddonConfig* thisPtr, uint a2, bool unk1, bool unk2) {
        FreeAllNodes();
        
        hudLayoutChangeHook!.Original(thisPtr, a2, unk1, unk2);
    }

    private void HandleEvent(AtkEventType eventType, AtkUnitBase* atkUnitBase, AtkResNode* node) {
        if (tooltipStrings.TryGetValue(node->NodeId, out var tooltipString)) {
            switch (eventType) {
                case AtkEventType.MouseOver: {
                    AtkStage.Instance()->TooltipManager.ShowTooltip(AddonMoney->Id, node, tooltipString);
                    break;
                }
                case AtkEventType.MouseOut:
                    AtkStage.Instance()->TooltipManager.HideTooltip(AddonMoney->Id);
                    break;
            }
        }
    }

    protected override void Disable() {
        openHudLayoutHook?.Disable();
        closeHudLayoutHook?.Disable();
        hudLayoutChangeHook?.Disable();
        updatePositionHook?.Disable();
        simpleEvent?.Dispose();
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Disable();
    }

    public override void Dispose() {
        openHudLayoutHook?.Dispose();
        closeHudLayoutHook?.Dispose();
        hudLayoutChangeHook?.Dispose();
        updatePositionHook?.Dispose();
        base.Dispose();
    }

    private void UpdatePosition(AtkResNode* baseNode, Vector2 resNodeSize, uint index, CurrencyEntry currencyInfo, ref Vector2 position) {
        if (TweakConfig.Grid) {
            // sanity check
            if (TweakConfig.GridSize < 2) TweakConfig.GridSize = 2;
            TweakConfig.GridGrowth = TweakConfig.DisplayDirection switch {
                Direction.Left or Direction.Right when TweakConfig.GridGrowth is Direction.Left or Direction.Right => Direction.Up,
                Direction.Up or Direction.Down when TweakConfig.GridGrowth is Direction.Up or Direction.Down => Direction.Left,
                _ => TweakConfig.GridGrowth
            };
            
            var columnIndex = TweakConfig.DisplayDirection switch {
                Direction.Up or Direction.Down => (index + 1) / TweakConfig.GridSize,
                Direction.Left or Direction.Right => (index + 1) % TweakConfig.GridSize,
                _ => throw new ArgumentOutOfRangeException(),
            };
            
            var columnSpacing = columnIndex == 0 || TweakConfig.GridSpacing.Length < columnIndex ? 0 : TweakConfig.GridSpacing[(int)columnIndex - 1];
            
            if ((index + 1) % TweakConfig.GridSize == 0) {

                position.X = TweakConfig.DisplayDirection switch {
                    Direction.Up => position.X + (TweakConfig.GridGrowth == Direction.Left ? -resNodeSize.X - columnSpacing : resNodeSize.X + columnSpacing),
                    Direction.Down => position.X + (TweakConfig.GridGrowth == Direction.Left ? -resNodeSize.X - columnSpacing : resNodeSize.X + columnSpacing),
                    Direction.Left => baseNode->X,
                    Direction.Right => baseNode->X,
                    _ => throw new ArgumentOutOfRangeException(),
                };
                
                position.Y = TweakConfig.DisplayDirection switch {
                    Direction.Up => baseNode->Y,
                    Direction.Down => baseNode->Y,
                    Direction.Left => position.Y + (TweakConfig.GridGrowth == Direction.Up ? -resNodeSize.Y : resNodeSize.Y),
                    Direction.Right => position.Y + (TweakConfig.GridGrowth == Direction.Up ? -resNodeSize.Y : resNodeSize.Y),
                    _ => throw new ArgumentOutOfRangeException(),
                };
                
            } else {
                position +=  TweakConfig.DisplayDirection switch {
                    Direction.Left => new Vector2(-resNodeSize.X - columnSpacing, 0),
                    Direction.Right => new Vector2(resNodeSize.X + columnSpacing, 0),
                    Direction.Up => new Vector2(0, -resNodeSize.Y),
                    Direction.Down => new Vector2(0, resNodeSize.Y),
                    _ => throw new ArgumentOutOfRangeException(),
                };
            }
        } else {
            position += TweakConfig.DisplayDirection switch {
                Direction.Left => new Vector2(-resNodeSize.X - currencyInfo.HorizontalSpacing, 0),
                Direction.Right => new Vector2(resNodeSize.X + currencyInfo.HorizontalSpacing, 0),
                Direction.Up => new Vector2(0, -resNodeSize.Y),
                Direction.Down => new Vector2(0, resNodeSize.Y),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }
    
    private void OnFrameworkUpdate() {
        if (!UiHelper.IsAddonReady(AddonMoney)) return;
        
        // Button Component Node
        var currencyPositionNode = Common.GetNodeByID(&AddonMoney->UldManager, 3);
        if (currencyPositionNode == null) return;
        Vector2 baseIconPosition;
        var iconPosition = baseIconPosition = new Vector2(currencyPositionNode->X, currencyPositionNode->Y);
        
        // Counter Node
        var counterPositionNode= Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, 2, NodeType.Counter);
        if (counterPositionNode == null) return;
        Vector2 baseCounterPosition;
        var counterPosition = baseCounterPosition = new Vector2(counterPositionNode->AtkResNode.X, counterPositionNode->AtkResNode.Y);
        
        // Size of one currency
        var resNodeSize = new Vector2(156, 36); // Hardcode it so we can change things
        
        // Resize Addon for Events
        if (!TweakConfig.DisableEvents) {
            AddonMoney->RootNode->SetHeight(1000);
            AddonMoney->RootNode->SetWidth(1000);
            AddonMoney->RootNode->SetPositionFloat(AddonMoney->X - 500 * AddonMoney->GetScale() * AddonMoney->RootNode->ScaleX, AddonMoney->Y - 500 * AddonMoney->GetScale() * AddonMoney->RootNode->ScaleY);
        
            currencyPositionNode->SetPositionFloat(620, 500);
            counterPositionNode->AtkResNode.SetPositionFloat(500, 508);
        }
        
        var gridIndex = 0U;
        
        // Make all counter nodes first, because if a icon node overlaps it even slightly it'll hide itself.
        foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count)) {
            var currencyInfo = TweakConfig.Currencies[(int)index].GetAdjusted();
            if (currencyInfo.Enabled == false) continue;
            
            if (currencyInfo.UseCustomPosition) {
                TryMakeCounterNode(CounterBaseId + index, baseCounterPosition + currencyInfo.CustomPosition, counterPositionNode->PartsList);
            } else {
                UpdatePosition(&counterPositionNode->AtkResNode, resNodeSize, gridIndex++, currencyInfo, ref counterPosition);
                TryMakeCounterNode(CounterBaseId + index, counterPosition, counterPositionNode->PartsList);
            }

            var count = currencyInfo.CollectibleItem 
                ? InventoryManager.Instance()->GetInventoryItemCount(currencyInfo.ItemId, minCollectability: 1)
                : InventoryManager.Instance()->GetInventoryItemCount(currencyInfo.ItemId);
            TryUpdateCounterNode(CounterBaseId + index, count);
        }
        
        gridIndex = 0;
        foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currencyInfo = TweakConfig.Currencies[(int) index].GetAdjusted();
            if (currencyInfo.Enabled == false) continue;   
            if (currencyInfo.UseCustomPosition) {
                TryMakeIconNode(ImageBaseId + index, baseIconPosition + currencyInfo.CustomPosition, currencyInfo.IconId, currencyInfo.HqItem, currencyInfo.Name);
            } else {
                UpdatePosition(currencyPositionNode, resNodeSize, gridIndex++, currencyInfo, ref iconPosition);
                TryMakeIconNode(ImageBaseId + index, iconPosition, currencyInfo.IconId, currencyInfo.HqItem, currencyInfo.Name);
            }
        }
    }

    private void DrawDirectionComboBox()
    {
        ImGui.TextUnformatted("Select which direction relative to Currency Display to show new items");
        
        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
        if (ImGui.BeginCombo("Direction", TweakConfig.DisplayDirection.ToString())) {
            foreach (var direction in Enum.GetValues<Direction>()) {
                if (ImGui.Selectable(direction.ToString(), TweakConfig.DisplayDirection == direction)) {
                    TweakConfig.DisplayDirection = direction;
                    TweakConfig.GridGrowth = direction switch {
                        Direction.Up when TweakConfig.GridGrowth is not Direction.Right => Direction.Left,
                        Direction.Down when TweakConfig.GridGrowth is not Direction.Right => Direction.Left,
                        Direction.Left when TweakConfig.GridGrowth is not Direction.Down => Direction.Up,
                        Direction.Right when TweakConfig.GridGrowth is not Direction.Down => Direction.Up,
                        _ => TweakConfig.GridGrowth,
                    };
                    
                    SaveConfig(TweakConfig);
                    FreeAllNodes();
                }
            }
            
            ImGui.EndCombo();
        }
    }

    private void DrawGridConfig() {

        if (ImGui.Checkbox("Disable Interaction", ref TweakConfig.DisableEvents)) {
            SaveConfig(TweakConfig);
            FreeAllNodes();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Disables tooltips for currency icons. \n - Should also help if you have issues with the UI moving unexpectedly.");

        ImGui.Checkbox("Use Grid Layout", ref TweakConfig.Grid);

        if (TweakConfig.Grid) {
            ImGui.Indent();
            var regionSize = ImGui.GetContentRegionAvail();
            
            ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
            if (ImGui.InputInt("Currencies Per Line", ref TweakConfig.GridSize)) {
                if (TweakConfig.GridSize < 2) TweakConfig.GridSize = 2;
                SaveConfig(TweakConfig);
            }
            if (TweakConfig.GridSize < 2) TweakConfig.GridSize = 2;
            
            ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
            if (ImGui.BeginCombo("Grid Growth Direction", TweakConfig.GridGrowth.ToString()))
            {
                foreach (var direction in Enum.GetValues<Direction>())
                {
                    if (TweakConfig.DisplayDirection is Direction.Up or Direction.Down && direction is Direction.Up or Direction.Down) continue;
                    if (TweakConfig.DisplayDirection is Direction.Left or Direction.Right && direction is Direction.Left or Direction.Right) continue;
                    
                    if (ImGui.Selectable(direction.ToString(), TweakConfig.DisplayDirection == direction))
                    {
                        TweakConfig.GridGrowth = direction;
                        SaveConfig(TweakConfig);
                    }
                }
            
                ImGui.EndCombo();
            }

            ImGui.Text("Grid Spacing: ");
            ImGui.Indent();

            for (var spacingIndex = 0; spacingIndex < TweakConfig.GridSpacing.Length; spacingIndex++) {

                if (ImGuiExt.IconButton($"removeColumnSpacing{spacingIndex}", FontAwesomeIcon.Minus)) {
                    var l = TweakConfig.GridSpacing.ToList();
                    l.RemoveAt(spacingIndex);
                    TweakConfig.GridSpacing = l.ToArray();
                    spacingIndex--;
                    continue;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove Column");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 2.0f / 3.0f);
                if (ImGui.DragFloat($"Column#{spacingIndex + 1}", ref TweakConfig.GridSpacing[spacingIndex], 0.5f)) {
                    SaveConfig(TweakConfig);
                }
            }

            if (ImGuiExt.IconButton("addGridSpacing", FontAwesomeIcon.Plus)) {
                Array.Resize(ref TweakConfig.GridSpacing, TweakConfig.GridSpacing.Length + 1);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Column");
            ImGui.Unindent();
            ImGui.Unindent();
        }
    }

    private void UpdateSearch() {
        if (searchString != string.Empty) {
            searchedItems = Service.Data.GetExcelSheet<Item>()!
                .OrderBy(item => item.ItemSortCategory.RowId)
                .Where(item => item.Name.ExtractText().Contains(searchString, StringComparison.CurrentCultureIgnoreCase))
                .Where(item => {
                    if (collectibleItemSearch) return item.IsCollectable;
                    if (hqItemSearch) return item.CanBeHq;
                    return !item.AlwaysCollectable;
                })
                .Take(10)
                .ToList();
        }
    }
    
    private void DrawAddCurrency() {
        ImGui.TextUnformatted("Search and select items to display");

        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
        if (ImGui.InputTextWithHint("###ItemSearch", "Search", ref searchString, 60, ImGuiInputTextFlags.AutoSelectAll)) {
            UpdateSearch();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("HQ Item", ref hqItemSearch)) {
            if (hqItemSearch) collectibleItemSearch = false;
            UpdateSearch();
        }
        ImGuiComponents.HelpMarker("To track HQ items such as Tinctures or Foods, enable 'HQ Item'");

        if (ImGui.BeginChild("###SearchResultChild", new Vector2(regionSize.X * 2.0f / 3.0f, 100.0f * ImGuiHelpers.GlobalScale), true)) {
            if (searchedItems.Count == 0) {
                var innerRegion = ImGui.GetContentRegionAvail();
                const string text = "Search for Items Above";
                var textSize = ImGui.CalcTextSize(text);
                
                ImGui.SetCursorPos(innerRegion / 2.0f - textSize / 2.0f);
                ImGui.TextUnformatted(text);
            }
            
            foreach (var item in searchedItems) {
                var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                    IconId = item.Icon, ItemHq = hqItemSearch,
                }).GetWrapOrDefault();
                
                if (icon is not null) {
                    if (ImGuiComponents.IconButton($"AddCurrencyButton{item.RowId}", FontAwesomeIcon.Plus)) {
                        TweakConfig.Currencies.Add(new CurrencyEntry {
                            IconId = item.Icon,
                            ItemId = hqItemSearch ? 1_000_000 + item.RowId : collectibleItemSearch ? 500_000 + item.RowId : item.RowId,
                            Name = item.Name.ExtractText() + (hqItemSearch ? " HQ" : string.Empty) + (collectibleItemSearch ? $" {(char)SeIconChar.Collectible}" : string.Empty),
                            HqItem = hqItemSearch,
                            CollectibleItem = collectibleItemSearch
                        });
                
                        FreeAllNodes();
                    }
                    ImGui.SameLine();
                    
                    ImGui.Image(icon.ImGuiHandle, new Vector2(23.0f, 23.0f));
                    ImGui.SameLine();
                    
                    ImGui.TextUnformatted($"{item.RowId:D6} - {item.Name.ExtractText()}");
                }
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.Checkbox("Collectible Item", ref collectibleItemSearch)) {
            if (collectibleItemSearch) hqItemSearch = false;
            UpdateSearch();
        }
        ImGuiComponents.HelpMarker("To track Collectible items.");
    }
    
    private void DrawCurrencies() {
        ImGui.TextUnformatted("Items being displayed:\n");
        var shownAutoGc = false;
        foreach (var index in Enumerable.Range(0, TweakConfig.Currencies.Count)) {
            var currency = TweakConfig.Currencies[index];
            if (ImGui.Checkbox($"##enableCurrency{index}", ref currency.Enabled)) {
                FreeAllNodes();
            }
            ImGui.SameLine();
            
            if (ImGuiComponents.IconButton($"RemoveCurrencyButton{index}", FontAwesomeIcon.Trash)) {
                FreeAllNodes();
                TweakConfig.Currencies.Remove(currency);
                break;
            }
            var iconButtonSize = ImGui.GetItemRectSize();
            ImGui.SameLine();

            if (index != 0) {
                if (ImGuiComponents.IconButton($"CurrencyUpButton{index}", FontAwesomeIcon.ArrowUp)) {
                    FreeAllNodes();
                    TweakConfig.Currencies.Remove(currency);
                    TweakConfig.Currencies.Insert(index - 1, currency);
                }
            }
            else {
                ImGui.Dummy(iconButtonSize);
            }
            ImGui.SameLine();

            if (index < TweakConfig.Currencies.Count - 1) {
                if (ImGuiComponents.IconButton($"CurrencyDownButton{index}", FontAwesomeIcon.ArrowDown)) {
                    FreeAllNodes();
                    TweakConfig.Currencies.Remove(currency);
                    TweakConfig.Currencies.Insert(index + 1, currency);
                }
            }
            else {
                ImGui.Dummy(iconButtonSize);
            }
            ImGui.SameLine();
            
            ImGui.BeginDisabled(!currency.Enabled);
            
            if (ImGuiExt.IconButton($"CurrencyPositionButton{index}", currency.UseCustomPosition ? FontAwesomeIcon.CompressArrowsAlt : FontAwesomeIcon.ExpandArrowsAlt)) {
                currency.UseCustomPosition = !currency.UseCustomPosition;
                SaveConfig(TweakConfig);
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(currency.UseCustomPosition ? "Return to main layout" : "Set a custom position");
            }
            
            ImGui.SameLine();
            
            if (TweakConfig.DisplayDirection is Direction.Left or Direction.Right && TweakConfig.Grid == false) {
                ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                ImGui.DragFloat($"##spacing_{index}", ref currency.HorizontalSpacing, 0.5f, currency.HorizontalSpacing - 100, currency.HorizontalSpacing + 100, "Spacing: %.0f");
                ImGui.SameLine();
            }

            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                IconId = (uint)currency.IconId, ItemHq = currency.HqItem,
            }).GetWrapOrDefault();
            
            if (icon is not null) {
                ImGui.Image(icon.ImGuiHandle, new Vector2(23.0f, 23.0f));
                ImGui.SameLine();
            }
            if (currency.UseCustomPosition) {
                
                var p = currency.CustomPosition;
                ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
                if (ImGui.DragFloat2($"##position_{index}", ref p)) {
                    currency.CustomPosition = p;
                }
                ImGui.SameLine();
            }
            
            ImGui.TextUnformatted(currency.Name);
            if (!shownAutoGc && currency.ItemId is 20 or 21 or 22) {
                shownAutoGc = true;
                ImGui.SameLine();
                if (ImGui.Checkbox("Automatically Select Grand Company", ref currency.AutoAdjustGrandCompany)) {
                    FreeAllNodes();
                    SaveConfig(TweakConfig);
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, grand company seals will be automatically adjusted to show your current grand company.");
                }
            }
            
            ImGui.EndDisabled();
        }
    }
    
    private void FreeAllNodes() {
        if (UiHelper.IsAddonReady(AddonMoney)) {
            AtkStage.Instance()->TooltipManager.HideTooltip(AddonMoney->Id);
            foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count)) {
                var iconNode = Common.GetNodeByID<AtkImageNode>(&AddonMoney->UldManager, ImageBaseId + index);
                if (iconNode is not null) {
                    simpleEvent?.Remove(AddonMoney, &iconNode->AtkResNode, AtkEventType.MouseOver);
                    simpleEvent?.Remove(AddonMoney, &iconNode->AtkResNode, AtkEventType.MouseOut);
                    UiHelper.UnlinkAndFreeImageNode(iconNode, AddonMoney);
                }
                
                var counterNode = Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, CounterBaseId + index);
                if (counterNode is not null) {
                    FreeCounterNode(counterNode);
                }
            }
            AddonMoney->UpdateCollisionNodeList(false);
            
            var currencyPositionNode = Common.GetNodeByID(&AddonMoney->UldManager, 3);
            var counterPositionNode= Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, 2, NodeType.Counter);
            
            AddonMoney->RootNode->SetHeight(36);
            AddonMoney->RootNode->SetWidth(156);
            AddonMoney->RootNode->SetPositionFloat(AddonMoney->X, AddonMoney->Y);
        
            if (currencyPositionNode != null) currencyPositionNode->SetPositionFloat(120, 0);
            if (counterPositionNode != null) counterPositionNode->AtkResNode.SetPositionFloat(0, 8);
        }
        tooltipStrings.Clear();
    }

    private static void TryUpdateCounterNode(uint nodeId, int newCount) {
        var counterNode = (AtkCounterNode*) Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is not null) {
            var numString = newCount.ToString("n0", CultureInfo.InvariantCulture);
            counterNode->SetText(Service.ClientState.ClientLanguage switch {
                ClientLanguage.German => numString.Replace(',', '.'),
                ClientLanguage.French => numString.Replace(',', ' '),
                _ => numString
            });
        }
    }
    
    private void TryMakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon, string? tooltipText = null) {
        var iconNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (iconNode is null) {
            MakeIconNode(nodeId, position, icon, hqIcon, tooltipText);
        }
        else {
            iconNode->SetPositionFloat(position.X, position.Y);
        }
    }

    private void TryMakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList) {
        var counterNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is null) {
            MakeCounterNode(nodeId, position, partsList);
        }
        else {
            counterNode->SetPositionFloat(position.X, position.Y);
        }
    }
    
    private void MakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon, string? tooltipText = null) {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 36, 36));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;

        imageNode->LoadIconTexture((uint)(hqIcon ? icon + 1_000_000 : icon), 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);
        imageNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);
        
        UiHelper.LinkNodeAtEnd((AtkResNode*) imageNode, AddonMoney);

        if (!TweakConfig.DisableEvents && tooltipText != null && simpleEvent != null) {
            imageNode->AtkResNode.NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
            AddonMoney->UpdateCollisionNodeList(false);
            simpleEvent?.Add(AddonMoney, &imageNode->AtkResNode, AtkEventType.MouseOver);
            simpleEvent?.Add(AddonMoney, &imageNode->AtkResNode, AtkEventType.MouseOut);
            tooltipStrings.TryAdd(nodeId, tooltipText);
        }
    }

    private void MakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList) {
        var counterNode = IMemorySpace.GetUISpace()->Create<AtkCounterNode>();
        counterNode->AtkResNode.Type = NodeType.Counter;
        counterNode->AtkResNode.NodeId = nodeId;
        counterNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;
        counterNode->AtkResNode.DrawFlags = 0;
        counterNode->NumberWidth = 10;
        counterNode->CommaWidth = 8;
        counterNode->SpaceWidth = 6;
        counterNode->TextAlign = 5;
        counterNode->PartsList = partsList;
        counterNode->AtkResNode.ToggleVisibility(true);
        
        counterNode->AtkResNode.SetWidth(128);
        counterNode->AtkResNode.SetHeight(22);
        counterNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);
        
        var node = AddonMoney->RootNode->ChildNode;
        while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

        node->PrevSiblingNode = (AtkResNode*) counterNode;
        counterNode->AtkResNode.NextSiblingNode = node;
        counterNode->AtkResNode.ParentNode = node->ParentNode;
        
        AddonMoney->UldManager.UpdateDrawNodeList();
    }

    private void FreeCounterNode(AtkCounterNode* node) {
        if (node != null) {
            if (node->AtkResNode.PrevSiblingNode != null)
                node->AtkResNode.PrevSiblingNode->NextSiblingNode = node->AtkResNode.NextSiblingNode;
            
            if (node->AtkResNode.NextSiblingNode != null)
                node->AtkResNode.NextSiblingNode->PrevSiblingNode = node->AtkResNode.PrevSiblingNode;
            
            AddonMoney->UldManager.UpdateDrawNodeList();
            node->AtkResNode.Destroy(false);
            IMemorySpace.Free(node, (ulong)sizeof(AtkCounterNode));
        }
    }
}