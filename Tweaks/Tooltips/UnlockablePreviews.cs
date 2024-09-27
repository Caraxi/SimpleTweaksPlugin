using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Companion = Lumina.Excel.GeneratedSheets2.Companion;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Preview Unlockable Items")]
[TweakDescription("Show a preview image for mounts, minions and hairstyles.")]
[TweakReleaseVersion("1.10.0.5")]
public unsafe class UnlockablePreviews : TooltipTweaks.SubTweak {
    private string lastImage;

    private string GetMountImagePath(uint mountId, bool hr = true) {
        var mount = Service.Data.GetExcelSheet<Mount>()?.GetRow(mountId);
        if (mount == null) return string.Empty;
        var id = mount.Icon + 64000U;
        return $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}{(hr ? "_hr1.tex" : ".tex")}";
    }

    private string GetMinionImagePath(uint minionId, bool hr = true) {
        var minion = Service.Data.GetExcelSheet<Companion>()?.GetRow(minionId);
        if (minion == null) return string.Empty;
        var id = minion.Icon + 64000U;
        return $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}{(hr ? "_hr1.tex" : ".tex")}";
    }

    private string GetHairstylePath(uint hairstyleItem, bool hr = true) {
        var character = (Character*)(Service.ClientState.LocalPlayer?.Address ?? 0);
        if (character == null) return string.Empty;

        var tribeId = character->DrawData.CustomizeData.Tribe;
        var sex = character->DrawData.CustomizeData.Sex;

        if (character->DrawObject != null && character->DrawObject->Object.GetObjectType() == ObjectType.CharacterBase) {
            var cb = (CharacterBase*)character->DrawObject;
            if (cb->GetModelType() == CharacterBase.ModelType.Human) {
                var human = (Human*)cb;
                tribeId = human->Customize.Tribe;
                sex = human->Customize.Sex;
            }
        }

        var type = Service.Data.GetExcelSheet<HairMakeTypeExt>()?.FirstOrDefault(t => t.Tribe.Row == tribeId && t.Gender == sex);
        var charaMakeCustomize = type?.HairStyles.FirstOrDefault(c => c.Value?.HintItem.Row == hairstyleItem);
        if (charaMakeCustomize?.Value == null) return string.Empty;

        var id = charaMakeCustomize.Value.Icon;
        return $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}{(hr ? "_hr1.tex" : ".tex")}";
    }

    private record ImageSize(float Width = 0, float Height = 0, float Scale = 1);

    [AddonPostRequestedUpdate("ItemDetail")]
    private void AfterItemDetailUpdate(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase == null) return;

        var imageNode = (AtkImageNode*)Common.GetNodeByID(&atkUnitBase->UldManager, CustomNodes.Get(this), NodeType.Image);
        if (imageNode != null) imageNode->AtkResNode.ToggleVisibility(false);

        var itemId = (uint)Service.GameGui.HoveredItem;
        if (itemId is >= 2000000 or <= 0) return;
        itemId %= 500000;
        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
        var itemAction = item?.ItemAction.Value;
        if (itemAction == null) return;

        var (imagePath, size) = itemAction.Type switch {
            1322 => (GetMountImagePath(itemAction.Data[0]), new ImageSize(190, 234, 0.8f)),
            853 => (GetMinionImagePath(itemAction.Data[0]), new ImageSize(100, 100, 0.8f)),
            2633 => (GetHairstylePath(itemId), new ImageSize(100, 100, 0.8f)),

            _ => (string.Empty, new ImageSize()),
        };

        if (imagePath == string.Empty) return;

        SimpleLog.Debug($"Image Path: {imagePath}");

        var insertNode = atkUnitBase->GetNodeById(2);
        if (insertNode == null) return;

        var anchorNode = atkUnitBase->GetNodeById(46);
        if (anchorNode == null) return;

        if (imageNode == null) {
            SimpleLog.Debug($"Create Image Node");

            imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
            imageNode->AtkResNode.Type = NodeType.Image;
            imageNode->AtkResNode.NodeId = CustomNodes.Get(this);
            imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
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

        if (imagePath != lastImage) {
            SimpleLog.Debug($"Load Texture: {imagePath}");
            if (imagePath != string.Empty) {
                imageNode->LoadTexture(imagePath);
            } else {
                imageNode->UnloadTexture();
            }

            lastImage = imagePath;
        }

        imageNode->AtkResNode.SetWidth((ushort)((atkUnitBase->RootNode->Width - 20f) * size.Scale));
        imageNode->AtkResNode.SetHeight((ushort)(imageNode->AtkResNode.Width * size.Width / size.Height));

        imageNode->AtkResNode.SetPositionFloat(atkUnitBase->RootNode->Width / 2f - imageNode->AtkResNode.Width / 2f, anchorNode->Y + anchorNode->GetHeight() + 8);
        atkUnitBase->WindowNode->AtkResNode.SetHeight((ushort)(imageNode->AtkResNode.Y + imageNode->AtkResNode.GetHeight() + 8));

        atkUnitBase->WindowNode->Component->UldManager.SearchNodeById(2)->SetHeight(atkUnitBase->WindowNode->AtkResNode.Height);
        insertNode->SetPositionFloat(insertNode->X, atkUnitBase->WindowNode->AtkResNode.GetHeight() - 20);
    }

    protected override void Disable() {
        lastImage = string.Empty;
        var unitBase = Common.GetUnitBase("ItemDetail");
        if (unitBase == null) return;
        var imageNode = (AtkImageNode*)Common.GetNodeByID(&unitBase->UldManager, CustomNodes.Get(this), NodeType.Image);
        if (imageNode == null) return;
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
