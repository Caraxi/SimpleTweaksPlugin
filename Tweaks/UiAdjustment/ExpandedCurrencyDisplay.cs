#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class ExpandedCurrencyDisplay : UiAdjustments.SubTweak
{
    public override string Name => "Expanded Currency Display";
    protected override string Author => "MidoriKami";
    public override string Description => "Allows you to display extra currencies.";

    public class Config : TweakConfig
    {
        public Direction DisplayDirection = Direction.Up;
        public List<CurrencyEntry> Currencies = new();
        public bool Grid;
        public int GridSize = 2;
        public float[] GridSpacing = Array.Empty<float>();
        public Direction GridGrowth = Direction.Left;
    }
    
    public class CurrencyEntry
    {
        public bool Enabled = true;
        public uint ItemId;
        public int IconId;
        public bool HqItem;
        public bool CollectibleItem;
        public string Name = string.Empty;
        public float HorizontalSpacing;
        public bool UseCustomPosition;
        public Vector2 CustomPosition;
    }

    public enum Direction 
    {
        Left,
        Right,
        Up,
        Down,
    }

    private Config TweakConfig { get; set; } = null!;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        DrawDirectionComboBox();
        
        DrawGridConfig();

        ImGuiHelpers.ScaledDummy(5.0f);
        DrawAddCurrency();
        
        ImGuiHelpers.ScaledDummy(5.0f);
        DrawCurrencies();
    };
    
    private static AtkUnitBase* AddonMoney => Common.GetUnitBase("_Money");

    private const uint ImageBaseId = 1000U;
    private const uint CounterBaseId = 2000U;
    private string searchString = string.Empty;
    private List<Item> searchedItems = new();
    private bool hqItemSearch;
    private bool collectibleItemSearch;
    private readonly Dictionary<uint, string> tooltipStrings = new();
    private SimpleEvent? simpleEvent;

    public override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        AddChangelog("1.8.3.1", "Use configured format culture for number display, should fix French issue");
        AddChangelog("1.8.4.0", "Added support for Collectibles");
        AddChangelog("1.8.8.0", "Added option for adjustable spacing in horizontal layouts.");
        AddChangelog("1.8.8.0", "Added option to display in a grid.");
        AddChangelog("1.8.8.0", "Added option to set the position of a currency individually.");
        AddChangelog("1.8.8.0", "Added tooltips when mouse is over the currency icons.");
        base.Setup();
    }

    public override void Enable()
    {
        simpleEvent ??= new SimpleEvent(HandleEvent);
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        if (TweakConfig.DisplayDirection is Direction.Up or Direction.Down && TweakConfig.GridGrowth is Direction.Up or Direction.Down) TweakConfig.GridGrowth = Direction.Left;
        if (TweakConfig.DisplayDirection is Direction.Left or Direction.Right && TweakConfig.GridGrowth is Direction.Left or Direction.Right) TweakConfig.GridGrowth = Direction.Up;
        
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    private void HandleEvent(AtkEventType eventType, AtkUnitBase* atkUnitBase, AtkResNode* node) {
        if (tooltipStrings.TryGetValue(node->NodeID, out var tooltipString)) {
            switch (eventType) {
                case AtkEventType.MouseOver: {
                    AtkStage.GetSingleton()->TooltipManager.ShowTooltip(AddonMoney->ID, node, tooltipString);
                    break;
                }
                case AtkEventType.MouseOut:
                    AtkStage.GetSingleton()->TooltipManager.HideTooltip(AddonMoney->ID);
                    break;
            }
        }
    }

    public override void Disable()
    {
        simpleEvent?.Dispose();
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Disable();
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
                _ => throw new ArgumentOutOfRangeException()
            };
            
            var columnSpacing = columnIndex == 0 || TweakConfig.GridSpacing.Length < columnIndex ? 0 : TweakConfig.GridSpacing[(int)columnIndex - 1];
            
            if ((index + 1) % TweakConfig.GridSize == 0) {

                position.X = TweakConfig.DisplayDirection switch {
                    Direction.Up => position.X + (TweakConfig.GridGrowth == Direction.Left ? -resNodeSize.X - columnSpacing : resNodeSize.X + columnSpacing),
                    Direction.Down => position.X + (TweakConfig.GridGrowth == Direction.Left ? -resNodeSize.X - columnSpacing : resNodeSize.X + columnSpacing),
                    Direction.Left => baseNode->X,
                    Direction.Right => baseNode->X,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                position.Y = TweakConfig.DisplayDirection switch {
                    Direction.Up => baseNode->Y,
                    Direction.Down => baseNode->Y,
                    Direction.Left => position.Y + (TweakConfig.GridGrowth == Direction.Up ? -resNodeSize.Y : resNodeSize.Y),
                    Direction.Right => position.Y + (TweakConfig.GridGrowth == Direction.Up ? -resNodeSize.Y : resNodeSize.Y),
                    _ => throw new ArgumentOutOfRangeException()
                };
                
            } else {
                position +=  TweakConfig.DisplayDirection switch {
                    Direction.Left => new Vector2(-resNodeSize.X - columnSpacing, 0),
                    Direction.Right => new Vector2(resNodeSize.X + columnSpacing, 0),
                    Direction.Up => new Vector2(0, -resNodeSize.Y),
                    Direction.Down => new Vector2(0, resNodeSize.Y),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        } else {
            position += TweakConfig.DisplayDirection switch {
                Direction.Left => new Vector2(-resNodeSize.X - currencyInfo.HorizontalSpacing, 0),
                Direction.Right => new Vector2(resNodeSize.X + currencyInfo.HorizontalSpacing, 0),
                Direction.Up => new Vector2(0, -resNodeSize.Y),
                Direction.Down => new Vector2(0, resNodeSize.Y),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
    
    private void OnFrameworkUpdate()
    {
        if (!UiHelper.IsAddonReady(AddonMoney)) return;
        using var _ = PerformanceMonitor.Run();

        
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
        AddonMoney->RootNode->SetHeight(1000);
        AddonMoney->RootNode->SetWidth(1000);
        AddonMoney->RootNode->SetPositionFloat(AddonMoney->X - 500, AddonMoney->Y - 500);
        
        currencyPositionNode->SetPositionFloat(620, 500);
        counterPositionNode->AtkResNode.SetPositionFloat(500, 508);

        var gridIndex = 0U;
        
        // Make all counter nodes first, because if a icon node overlaps it even slightly it'll hide itself.
        foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currencyInfo = TweakConfig.Currencies[(int) index];
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
            var currencyInfo = TweakConfig.Currencies[(int) index];
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
        if (ImGui.BeginCombo("Direction", TweakConfig.DisplayDirection.ToString()))
        {
            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (ImGui.Selectable(direction.ToString(), TweakConfig.DisplayDirection == direction))
                {
                    TweakConfig.DisplayDirection = direction;
                    TweakConfig.GridGrowth = direction switch {
                        Direction.Up when TweakConfig.GridGrowth is not Direction.Right => Direction.Left,
                        Direction.Down when TweakConfig.GridGrowth is not Direction.Right => Direction.Left,
                        Direction.Left when TweakConfig.GridGrowth is not Direction.Down => Direction.Up,
                        Direction.Right when TweakConfig.GridGrowth is not Direction.Down => Direction.Up,
                        _ => TweakConfig.GridGrowth
                    };
                    
                    SaveConfig(TweakConfig);
                    FreeAllNodes();
                }
            }
            
            ImGui.EndCombo();
        }
    }

    private void DrawGridConfig() {

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

    private void UpdateSearch() 
    {
        if (searchString != string.Empty)
        {
            searchedItems = Service.Data.GetExcelSheet<Item>()!
                .OrderBy(item => item.ItemSortCategory.Row)
                .Where(item => item.Name.ToDalamudString().TextValue.ToLower().Contains(searchString.ToLower()))
                .Where(item => {
                    if (collectibleItemSearch) return item.IsCollectable;
                    if (hqItemSearch) return item.CanBeHq;
                    return !item.AlwaysCollectable;
                })
                .Take(10)
                .ToList();
        }
    }
    
    private void DrawAddCurrency()
    {
        ImGui.TextUnformatted("Search and select items to display");

        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
        if (ImGui.InputTextWithHint("###ItemSearch", "Search", ref searchString, 60, ImGuiInputTextFlags.AutoSelectAll))
        {
            UpdateSearch();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("HQ Item", ref hqItemSearch)) {
            if (hqItemSearch) collectibleItemSearch = false;
            UpdateSearch();
        }
        ImGuiComponents.HelpMarker("To track HQ items such as Tinctures or Foods, enable 'HQ Item'");

        if (ImGui.BeginChild("###SearchResultChild", new Vector2(regionSize.X * 2.0f / 3.0f, 100.0f * ImGuiHelpers.GlobalScale), true))
        {
            if (searchedItems.Count == 0)
            {
                var innerRegion = ImGui.GetContentRegionAvail();
                var text = "Search for Items Above";
                var textSize = ImGui.CalcTextSize(text);
                
                ImGui.SetCursorPos(innerRegion / 2.0f - textSize / 2.0f);
                ImGui.TextUnformatted(text);
            }
            
            foreach (var item in searchedItems)
            {
                var icon = Plugin.IconManager.GetIconTexture(item.Icon, hqItemSearch);
                if (icon is not null)
                {
                    if (ImGuiComponents.IconButton($"AddCurrencyButton{item.RowId}", FontAwesomeIcon.Plus))
                    {
                        TweakConfig.Currencies.Add(new CurrencyEntry
                        {
                            IconId = item.Icon,
                            ItemId = hqItemSearch ? 1_000_000 + item.RowId : collectibleItemSearch ? 500_000 + item.RowId : item.RowId,
                            Name = item.Name.ToDalamudString() + (hqItemSearch ? " HQ" : string.Empty) + (collectibleItemSearch ? $" {(char)SeIconChar.Collectible}" : string.Empty),
                            HqItem = hqItemSearch,
                            CollectibleItem = collectibleItemSearch
                        });
                
                        FreeAllNodes();
                    }
                    ImGui.SameLine();
                    
                    ImGui.Image(icon.ImGuiHandle, new Vector2(23.0f, 23.0f));
                    ImGui.SameLine();
                    
                    ImGui.TextUnformatted($"{item.RowId:D6} - {item.Name.ToDalamudString()}");
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
    
    private void DrawCurrencies()
    {
        ImGui.TextUnformatted("Items being displayed:\n");
        
        foreach (var index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currency = TweakConfig.Currencies[index];
            if (ImGui.Checkbox($"##enableCurrency{index}", ref currency.Enabled)) {
                FreeAllNodes();
            }
            ImGui.SameLine();
            
            if (ImGuiComponents.IconButton($"RemoveCurrencyButton{index}", FontAwesomeIcon.Trash))
            {
                FreeAllNodes();
                TweakConfig.Currencies.Remove(currency);
                break;
            }
            var iconButtonSize = ImGui.GetItemRectSize();
            ImGui.SameLine();

            if (index != 0)
            {
                if (ImGuiComponents.IconButton($"CurrencyUpButton{index}", FontAwesomeIcon.ArrowUp)) {
                    FreeAllNodes();
                    TweakConfig.Currencies.Remove(currency);
                    TweakConfig.Currencies.Insert(index - 1, currency);
                }
            }
            else
            {
                ImGui.Dummy(iconButtonSize);
            }
            ImGui.SameLine();

            if (index < TweakConfig.Currencies.Count - 1)
            {
                if (ImGuiComponents.IconButton($"CurrencyDownButton{index}", FontAwesomeIcon.ArrowDown))
                {
                    FreeAllNodes();
                    TweakConfig.Currencies.Remove(currency);
                    TweakConfig.Currencies.Insert(index + 1, currency);
                }
            }
            else
            {
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
            
            if (TweakConfig.DisplayDirection is Direction.Left or Direction.Right && TweakConfig.Grid == false)
            {
                ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                ImGui.DragFloat($"##spacing_{index}", ref currency.HorizontalSpacing, 0.5f, currency.HorizontalSpacing - 100, currency.HorizontalSpacing + 100, "Spacing: %.0f");
                ImGui.SameLine();
            }

            var icon = Plugin.IconManager.GetIconTexture(currency.IconId, currency.HqItem);
            if (icon is not null)
            {
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
            ImGui.EndDisabled();
        }
    }
    
    private void FreeAllNodes()
    {
        if (UiHelper.IsAddonReady(AddonMoney))
        {
            AtkStage.GetSingleton()->TooltipManager.HideTooltip(AddonMoney->ID);
            foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
            {
                var iconNode = Common.GetNodeByID<AtkImageNode>(&AddonMoney->UldManager, ImageBaseId + index);
                if (iconNode is not null)
                {
                    simpleEvent?.Remove(AddonMoney, &iconNode->AtkResNode, AtkEventType.MouseOver);
                    simpleEvent?.Remove(AddonMoney, &iconNode->AtkResNode, AtkEventType.MouseOut);
                    UiHelper.UnlinkAndFreeImageNode(iconNode, AddonMoney);
                }
                
                var counterNode = Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, CounterBaseId + index);
                if (counterNode is not null)
                {
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

    private void TryUpdateCounterNode(uint nodeId, int newCount)
    {
        var counterNode = (AtkCounterNode*) Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is not null)
        {
            counterNode->SetText(newCount.ToString("n0", Plugin.Culture));
        }
    }
    
    private void TryMakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon, string? tooltipText = null)
    {
        var iconNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (iconNode is null)
        {
            MakeIconNode(nodeId, position, icon, hqIcon, tooltipText);
        }
        else
        {
            iconNode->SetPositionFloat(position.X, position.Y);
        }
    }

    private void TryMakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList)
    {
        var counterNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is null)
        {
            MakeCounterNode(nodeId, position, partsList);
        }
        else
        {
            counterNode->SetPositionFloat(position.X, position.Y);
        }
    }
    
    private void MakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon, string? tooltipText = null)
    {
        var imageNode = UiHelper.MakeImageNode(nodeId, new UiHelper.PartInfo(0, 0, 36, 36));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;

        imageNode->LoadIconTexture(hqIcon ? icon + 1_000_000 : icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);
        imageNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);
        
        UiHelper.LinkNodeAtEnd((AtkResNode*) imageNode, AddonMoney);

        if (tooltipText != null && simpleEvent != null) {
            imageNode->AtkResNode.NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
            AddonMoney->UpdateCollisionNodeList(false);
            simpleEvent?.Add(AddonMoney, &imageNode->AtkResNode, AtkEventType.MouseOver);
            simpleEvent?.Add(AddonMoney, &imageNode->AtkResNode, AtkEventType.MouseOut);
            tooltipStrings.TryAdd(nodeId, tooltipText);
        }
    }

    private void MakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList)
    {
        var counterNode = IMemorySpace.GetUISpace()->Create<AtkCounterNode>();
        counterNode->AtkResNode.Type = NodeType.Counter;
        counterNode->AtkResNode.NodeID = nodeId;
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

    private void FreeCounterNode(AtkCounterNode* node)
    {
        if (node != null)
        {
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