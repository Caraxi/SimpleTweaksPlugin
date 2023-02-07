using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class MoreMoney : UiAdjustments.SubTweak
{
    public override string Name => "MoMoney";
    protected override string Author => "MidoriKami";
    public override string Description => "Allows you to display extra currencies.";
    private static AtkUnitBase* AddonMoney => Common.GetUnitBase("_Money");

    private short count;
    
    public override void Enable()
    {
        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        
        if (AddonMoney is not null)
        {
            TryFreeIconNode(1000);
            TryFreeIconNode(1001);
            
            TryFreeCounterNode(2000);
            TryFreeCounterNode(2001);
        }
        base.Disable();
    }

    public override void Dispose()
    {
        Common.FrameworkUpdate -= OnFrameworkUpdate;

        if (AddonMoney is not null)
        {
            TryFreeIconNode(1000);
            TryFreeIconNode(1001);
            
            TryFreeCounterNode(2000);
            TryFreeCounterNode(2001);
        }
        base.Dispose();
    }
    
    private void OnFrameworkUpdate()
    {
        if (AddonMoney is null) return;

        // Button Component Node
        var currencyPositionNode = Common.GetNodeByID(&AddonMoney->UldManager, 3);
        var currencyPosition = new Vector2(currencyPositionNode->X, currencyPositionNode->Y);
        var currencySize = new Vector2(currencyPositionNode->Width, currencyPositionNode->Height);
        
        // Counter Node
        var counterPositionNode= Common.GetNodeByID(&AddonMoney->UldManager, 2);
        var counterPosition = new Vector2(counterPositionNode->X, counterPositionNode->Y);
        var counterSize = new Vector2(counterPositionNode->Width, counterPositionNode->Height);
        
        TryMakeCounterNode(2000, counterPosition + new Vector2(0, -currencySize.Y));
        TryMakeCounterNode(2001, counterPosition + new Vector2(0, -currencySize.Y * 2.0f));
        
        TryMakeIconNode(1000, currencyPosition + new Vector2(0, -currencySize.Y));
        TryMakeIconNode(1001, currencyPosition + new Vector2(0, -currencySize.Y * 2));
        
        TryUpdateCounterNode(2000, count++);
        TryUpdateCounterNode(2001, count * 2);
    }

    private void TryUpdateCounterNode(uint nodeId, int newCount)
    {
        var counterNode = (AtkCounterNode*) Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is not null)
        {
            var countString = $"{newCount:n0}";
            var numCommas = countString.Count(character => character == ',');
            var numNumbers = countString.Length - numCommas;

            var width = 10 * numNumbers + 8 * numCommas + 6 + 5;
            
            counterNode->NodeText.SetString($"{newCount:N0}");
            counterNode->Width = width;
        }
    }

    private void TryMakeIconNode(uint nodeId, Vector2 position)
    {
        var iconNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (iconNode is null)
        {
            MakeIconNode(nodeId, position);
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

    private void TryMakeCounterNode(uint nodeId, Vector2 position)
    {
        var counterNode = Common.GetNodeByID(&AddonMoney->UldManager, nodeId);
        if (counterNode is null)
        {
            MakeCounterNode(nodeId, position);
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
    
    private void MakeIconNode(uint nodeId, Vector2 position)
    {
        var imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
        imageNode->AtkResNode.Type = NodeType.Image;
        imageNode->AtkResNode.NodeID = nodeId;
        imageNode->AtkResNode.Flags = 8243;
        imageNode->AtkResNode.DrawFlags = 0;
        imageNode->WrapMode = 1;
        imageNode->Flags = 0;
        
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
        
        imageNode->LoadIconTexture(65087, 0);
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

    private void MakeCounterNode(uint nodeId, Vector2 position)
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
        
        var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        if (partsList == null) 
        {
            SimpleLog.Error("Failed to alloc memory for parts list.");
            counterNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(counterNode, (ulong)sizeof(AtkCounterNode));
            return;
        }
        
        partsList->Id = 1;
        partsList->PartCount = 1;
        
        var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
        if (part == null) 
        {
            SimpleLog.Error("Failed to alloc memory for part.");
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            counterNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(counterNode, (ulong)sizeof(AtkCounterNode));
            return;
        }

        part->U = 0;
        part->V = 0;
        part->Width = 22;
        part->Height = 22;
        
        partsList->Parts = part;

        var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
        if (asset == null) 
        {
            SimpleLog.Error("Failed to alloc memory for asset.");
            IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            counterNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(counterNode, (ulong)sizeof(AtkCounterNode));
            return;
        }

        asset->Id = 1;
        asset->AtkTexture.Ctor();
        part->UldAsset = asset;
        counterNode->PartsList = partsList;
        
        ((AtkImageNode*)counterNode)->LoadTexture("ui/uld/money_number_hr1.tex");
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

            IMemorySpace.Free(counterNode->PartsList->Parts->UldAsset, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(counterNode->PartsList->Parts, (ulong) sizeof(AtkUldPart));
            IMemorySpace.Free(counterNode->PartsList, (ulong) sizeof(AtkUldPartsList));
            counterNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(counterNode, (ulong)sizeof(AtkCounterNode));
        }
    }
}