using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public unsafe class PaintingPreview : TooltipTweaks.SubTweak {
    
    [Sheet("Picture")]
    public class FixedPicture : ExcelRow {
        public int Image { get; set; }

        public override void PopulateData( RowParser parser, GameData gameData, Language language )
        {
            base.PopulateData( parser, gameData, language );
            Image = parser.ReadColumn< int >( 0 );
        }
    }
    
    public override string Name => "Show Painting Preview";
    public override string Description => "Add an image preview for paintings to item tooltips.";

    private HookWrapper<Common.AddonOnUpdate> itemTooltipOnUpdateHook;

    public override void Enable() {
        itemTooltipOnUpdateHook ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", AfterItemDetailUpdate);
        itemTooltipOnUpdateHook?.Enable();        
        base.Enable();
    }

    private int lastImage;
    
    private void AfterItemDetailUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        if (atkUnitBase == null) return;
        var imageNode = (AtkImageNode*) Common.GetNodeByID(&atkUnitBase->UldManager, CustomNodes.PaintingPreview, NodeType.Image);
        if (imageNode != null) imageNode->AtkResNode.ToggleVisibility(false);
        
        var itemId = (uint)Service.GameGui.HoveredItem;
        if (itemId is >= 2000000 or <= 0) return;
        itemId %= 500000;
        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
        if (item == null) return;
        if (item.ItemUICategory.Row != 95) return;
        var picture = Service.Data.Excel.GetSheet<FixedPicture>()?.GetRow(item.AdditionalData);
        if (picture == null) return;

        var insertNode = atkUnitBase->GetNodeById(2);
        if (insertNode == null) return;
        
        if (imageNode == null) {
            SimpleLog.Log($"Create Image Node");

            imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
            imageNode->AtkResNode.Type = NodeType.Image;
            imageNode->AtkResNode.NodeID = CustomNodes.PaintingPreview;
            imageNode->AtkResNode.Flags = (short)(NodeFlags.AnchorTop | NodeFlags.AnchorLeft);
            imageNode->AtkResNode.DrawFlags = 0;
            imageNode->WrapMode = 1;
            imageNode->Flags = 128;

            var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
            if (partsList == null) {
                SimpleLog.Error("Failed to alloc memory for parts list.");
                imageNode->AtkResNode.Destroy(true);
                return;
            }

            partsList->Id = 0;
            partsList->PartCount = 1;

            var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
            if (part == null) {
                SimpleLog.Error("Failed to alloc memory for part.");
                IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                imageNode->AtkResNode.Destroy(true);
                return;
            }

            part->U = 0;
            part->V = 0;
            part->Width = 256;
            part->Height = 256;

            partsList->Parts = part;

            var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
            if (asset == null) {
                SimpleLog.Error("Failed to alloc memory for asset.");
                IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
                IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                imageNode->AtkResNode.Destroy(true);
                return;
            }

            asset->Id = 0;
            asset->AtkTexture.Ctor();
            part->UldAsset = asset;
            imageNode->PartsList = partsList;

            imageNode->AtkResNode.ToggleVisibility(true);
            
            var prev = insertNode->PrevSiblingNode;
            imageNode->AtkResNode.ParentNode = insertNode->ParentNode;

            insertNode->PrevSiblingNode = (AtkResNode*)imageNode;
            
            if (prev != null) prev->NextSiblingNode = (AtkResNode*)imageNode;

            imageNode->AtkResNode.PrevSiblingNode = prev;
            imageNode->AtkResNode.NextSiblingNode = insertNode;

            atkUnitBase->UldManager.UpdateDrawNodeList();
        }
        
        if (imageNode == null) return;
        imageNode->AtkResNode.ToggleVisibility(true);
        

        if (picture.Image != lastImage) {
            var texPath = $"ui/icon/{picture.Image / 1000 * 1000:000000}/{picture.Image:000000}.tex";
            SimpleLog.Log($"Load Texture: {texPath}");
            imageNode->LoadTexture(texPath);
            lastImage = picture.Image;
        }
        
        imageNode->AtkResNode.SetWidth((ushort)(atkUnitBase->RootNode->Width - 20f));
        imageNode->AtkResNode.SetHeight((ushort)(imageNode->AtkResNode.Width * 0.6f));
        
        imageNode->AtkResNode.SetPositionFloat(atkUnitBase->RootNode->Width / 2f - imageNode->AtkResNode.Width / 2f, atkUnitBase->WindowNode->AtkResNode.Height - 10f);
        atkUnitBase->WindowNode->AtkResNode.SetHeight((ushort)(atkUnitBase->WindowNode->AtkResNode.Height + imageNode->AtkResNode.Height));
        
        atkUnitBase->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(atkUnitBase->WindowNode->AtkResNode.Height);
        insertNode->SetPositionFloat(insertNode->X, insertNode->Y + imageNode->AtkResNode.Height);
    }
    
    public override void Disable() {
        itemTooltipOnUpdateHook?.Disable();
        lastImage = 0;
        var unitBase = Common.GetUnitBase("ItemDetail");
        if (unitBase != null) {
            var imageNode = (AtkImageNode*) Common.GetNodeByID(&unitBase->UldManager, CustomNodes.PaintingPreview, NodeType.Image);
            if (imageNode != null) {
                if (imageNode->AtkResNode.PrevSiblingNode != null)
                    imageNode->AtkResNode.PrevSiblingNode->NextSiblingNode = imageNode->AtkResNode.NextSiblingNode;
                if (imageNode->AtkResNode.NextSiblingNode != null)
                    imageNode->AtkResNode.NextSiblingNode->PrevSiblingNode = imageNode->AtkResNode.PrevSiblingNode;
                unitBase->UldManager.UpdateDrawNodeList();

                IMemorySpace.Free(imageNode->PartsList->Parts->UldAsset, (ulong)sizeof(AtkUldPart));
                IMemorySpace.Free(imageNode->PartsList->Parts, (ulong)sizeof(AtkUldPart));
                IMemorySpace.Free(imageNode->PartsList, (ulong)sizeof(AtkUldPartsList));
                imageNode->AtkResNode.Destroy(true);
            }
        }
        
        base.Disable();
    }

    public override void Dispose() {
        itemTooltipOnUpdateHook?.Dispose();
        base.Dispose();
    }
}
