#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
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
    }
    
    public class CurrencyEntry
    {
        public uint ItemId;
        public int IconId;
        public bool HqItem;
        public string Name = string.Empty;
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

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Disable();
    }

    public override void Dispose()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        FreeAllNodes();
        base.Dispose();
    }

    private void OnFrameworkUpdate()
    {
        if (AddonMoney is null) return;

        // Size of one currency
        var resNodeSize = new Vector2(AddonMoney->RootNode->Width, AddonMoney->RootNode->Height);
        
        // Button Component Node
        var currencyPositionNode = Common.GetNodeByID(&AddonMoney->UldManager, 3);
        if (currencyPositionNode == null) return;
        var currencyBasePosition = new Vector2(currencyPositionNode->X, currencyPositionNode->Y);
        
        // Counter Node
        var counterPositionNode= Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, 2, NodeType.Counter);
        if (counterPositionNode == null) return;
        var counterBasePosition = new Vector2(counterPositionNode->AtkResNode.X, counterPositionNode->AtkResNode.Y);

        // Make all counter nodes first, because if a icon node overlaps it even slightly it'll hide itself.
        foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currencyInfo = TweakConfig.Currencies[(int) index];

            var counterPosition = TweakConfig.DisplayDirection switch
            {
                Direction.Left => counterBasePosition + new Vector2(-resNodeSize.X * (index + 1), 0),
                Direction.Right => counterBasePosition + new Vector2(resNodeSize.X * (index + 1), 0),
                Direction.Up => counterBasePosition + new Vector2(0, -resNodeSize.Y * (index + 1)),
                Direction.Down => counterBasePosition + new Vector2(0, resNodeSize.Y * (index + 1)),
                _ => throw new ArgumentOutOfRangeException()
            };

            TryMakeCounterNode(CounterBaseId + index, counterPosition, counterPositionNode->PartsList);
            TryUpdateCounterNode(CounterBaseId + index, InventoryManager.Instance()->GetInventoryItemCount(currencyInfo.ItemId));
        }
        
        foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currencyInfo = TweakConfig.Currencies[(int) index];

            var iconPosition = TweakConfig.DisplayDirection switch
            {
                Direction.Left => currencyBasePosition + new Vector2(-resNodeSize.X * (index + 1), 0),
                Direction.Right => currencyBasePosition + new Vector2(resNodeSize.X * (index + 1), 0),
                Direction.Up => currencyBasePosition + new Vector2(0, -resNodeSize.Y * (index + 1)),
                Direction.Down => currencyBasePosition + new Vector2(0, resNodeSize.Y * (index + 1)),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            TryMakeIconNode(ImageBaseId + index, iconPosition, currencyInfo.IconId, currencyInfo.HqItem);
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
                    SaveConfig(TweakConfig);
                    FreeAllNodes();
                }
            }
            
            ImGui.EndCombo();
        }
    }

    private void DrawAddCurrency()
    {
        ImGui.TextUnformatted("Search and select items to display");
        
        var regionSize = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(regionSize.X * 2.0f / 3.0f);
        if (ImGui.InputTextWithHint("###ItemSearch", "Search", ref searchString, 60, ImGuiInputTextFlags.AutoSelectAll))
        {
            if (searchString != string.Empty)
            {
                searchedItems = Service.Data.GetExcelSheet<Item>()!
                    .OrderBy(item => item.ItemSortCategory.Row)
                    .Where(item => item.Name.ToDalamudString().TextValue.ToLower().Contains(searchString.ToLower()))
                    .Take(10)
                    .ToList();
            }
        }
        ImGui.SameLine();
        ImGui.Checkbox("HQ Item", ref hqItemSearch);
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
                            ItemId = hqItemSearch ? 1_000_000 + item.RowId : item.RowId,
                            Name = item.Name.ToDalamudString() + (hqItemSearch ? " HQ" : string.Empty),
                            HqItem = hqItemSearch,
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
    }
    
    private void DrawCurrencies()
    {
        ImGui.TextUnformatted("Items being displayed:\n");
        
        foreach (var index in Enumerable.Range(0, TweakConfig.Currencies.Count))
        {
            var currency = TweakConfig.Currencies[index];
            
            if (ImGuiComponents.IconButton($"RemoveCurrencyButton{index}", FontAwesomeIcon.Trash))
            {
                FreeAllNodes();
                TweakConfig.Currencies.Remove(currency);
                break;
            }
            ImGui.SameLine();
            
            var icon = Plugin.IconManager.GetIconTexture(currency.IconId, currency.HqItem);
            if (icon is not null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(23.0f, 23.0f));
                ImGui.SameLine();
            }
            
            ImGui.TextUnformatted(currency.Name);
        }
    }
    
    private void FreeAllNodes()
    {
        if (AddonMoney is not null)
        {
            foreach (uint index in Enumerable.Range(0, TweakConfig.Currencies.Count))
            {
                TryFreeIconNode(ImageBaseId + index);
                TryFreeCounterNode(CounterBaseId + index);
            }
        }
    }

    private void TryUpdateCounterNode(uint nodeId, int newCount)
    {
        var counterNode = (AtkCounterNode*) Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is not null)
        {
            counterNode->SetText(newCount.ToString("n0"));
        }
    }
    
    private void TryMakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon)
    {
        var iconNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (iconNode is null)
        {
            MakeIconNode(nodeId, position, icon, hqIcon);
        }
    }

    private void TryFreeIconNode(uint nodeId)
    {
        var iconNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (iconNode is not null)
        {
            FreeImageNode(nodeId);
        }
    }

    private void TryMakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList)
    {
        var counterNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is null)
        {
            MakeCounterNode(nodeId, position, partsList);
        }
    }

    private void TryFreeCounterNode(uint nodeId)
    {
        var counterNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is not null)
        {
            FreeCounterNode(nodeId);
        }
    }
    
    private void MakeIconNode(uint nodeId, Vector2 position, int icon, bool hqIcon)
    {
        var imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
        imageNode->AtkResNode.Type = NodeType.Image;
        imageNode->AtkResNode.NodeID = nodeId;
        imageNode->AtkResNode.Flags = 8243;
        imageNode->AtkResNode.DrawFlags = 0;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte) ImageNodeFlags.AutoFit;
        
        var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        if (partsList == null) 
        {
            SimpleLog.Error("Failed to alloc memory for parts list.");
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }
        
        partsList->Id = 0;
        partsList->PartCount = 1;

        var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
        if (part == null) 
        {
            SimpleLog.Error("Failed to alloc memory for part.");
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }

        part->U = 0;
        part->V = 0;
        part->Width = 36;
        part->Height = 36;

        partsList->Parts = part;

        var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
        if (asset == null) 
        {
            SimpleLog.Error("Failed to alloc memory for asset.");
            IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return;
        }

        asset->Id = 0;
        asset->AtkTexture.Ctor();
        part->UldAsset = asset;
        imageNode->PartsList = partsList;
        
        imageNode->LoadIconTexture(hqIcon ? icon + 1_000_000 : icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(36);
        imageNode->AtkResNode.SetHeight(36);
        imageNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);
        
        var node = AddonMoney->RootNode->ChildNode;
        while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

        node->PrevSiblingNode = (AtkResNode*) imageNode;
        imageNode->AtkResNode.NextSiblingNode = node;
        imageNode->AtkResNode.ParentNode = node->ParentNode;
        
        AddonMoney->UldManager.UpdateDrawNodeList();
    }

    private void MakeCounterNode(uint nodeId, Vector2 position, AtkUldPartsList* partsList)
    {
        var counterNode = IMemorySpace.GetUISpace()->Create<AtkCounterNode>();
        counterNode->AtkResNode.Type = NodeType.Counter;
        counterNode->AtkResNode.NodeID = nodeId;
        counterNode->AtkResNode.Flags = 8243;
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

    private void FreeImageNode(uint nodeId)
    {
        var imageNode = Common.GetNodeByID<AtkImageNode>(&AddonMoney->UldManager, nodeId, NodeType.Image);
        if (imageNode != null)
        {
            if (imageNode->AtkResNode.PrevSiblingNode != null)
                imageNode->AtkResNode.PrevSiblingNode->NextSiblingNode = imageNode->AtkResNode.NextSiblingNode;
            
            if (imageNode->AtkResNode.NextSiblingNode != null)
                imageNode->AtkResNode.NextSiblingNode->PrevSiblingNode = imageNode->AtkResNode.PrevSiblingNode;
            
            AddonMoney->UldManager.UpdateDrawNodeList();

            IMemorySpace.Free(imageNode->PartsList->Parts->UldAsset, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(imageNode->PartsList->Parts, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(imageNode->PartsList, (ulong) sizeof(AtkUldPartsList));
            imageNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
        }
    }

    private void FreeCounterNode(uint nodeId)
    {
        var counterNode = Common.GetNodeByID<AtkCounterNode>(&AddonMoney->UldManager, nodeId, NodeType.Counter);
        if (counterNode != null)
        {
            if (counterNode->AtkResNode.PrevSiblingNode != null)
                counterNode->AtkResNode.PrevSiblingNode->NextSiblingNode = counterNode->AtkResNode.NextSiblingNode;
            
            if (counterNode->AtkResNode.NextSiblingNode != null)
                counterNode->AtkResNode.NextSiblingNode->PrevSiblingNode = counterNode->AtkResNode.PrevSiblingNode;
            
            AddonMoney->UldManager.UpdateDrawNodeList();
            counterNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(counterNode, (ulong)sizeof(AtkCounterNode));
        }
    }
}