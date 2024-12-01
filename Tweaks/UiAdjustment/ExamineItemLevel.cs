using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Item Level in Examine")]
[TweakDescription("Calculates the item level of other players when examining them.\nRed value means the player is wearing an item that scales to their level and it is showing the max level.")]
[TweakAutoConfig]
public unsafe class ExamineItemLevel : UiAdjustments.SubTweak {
    public class Config : TweakConfig {
        [TweakConfigOption("Show Item Level Icon")]
        public bool ShowItemLevelIcon = true;
    }

    [TweakConfig] public Config TweakConfig { get; private set; }

    private readonly uint[] canHaveOffhand = [2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];
    private readonly uint[] ignoreCategory = [105];

    [AddonPostRefresh("CharacterInspect")]
    private void ExamineRefresh(AddonRefreshArgs args) {
        if (args.AtkValueCount > 0 && args.AtkValueSpan[0].UInt == 4) {
            ShowItemLevel();
            Service.Framework.RunOnTick(ShowItemLevel, TimeSpan.FromMilliseconds(250));
        }
    }

    private void ShowItemLevel() {
        try {
            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
            if (container == null) return;
            var examineWindow = (AddonCharacterInspect*)Common.GetUnitBase("CharacterInspect");
            if (examineWindow == null) return;
            var previewComponent = examineWindow->PreviewComponent;
            var compInfo = (AtkUldComponentInfo*)previewComponent->UldManager.Objects;
            if (compInfo == null || compInfo->ComponentType != ComponentType.Preview) return;

            var inaccurate = false;
            var sum = 0U;
            var c = 12;
            for (var i = 0; i < 13; i++) {
                if (i == 5) continue;
                var slot = container->GetInventorySlot(i);
                if (slot == null) continue;
                var id = slot->ItemId;
                var item = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(id);
                if (item == null) continue;
                if (ignoreCategory.Contains(item.Value.ItemUICategory.RowId)) {
                    if (i == 0) c -= 1;
                    c -= 1;
                    continue;
                }

                if (item.Value.SubStatCategory == 2) inaccurate = true;
                if (i == 0 && !canHaveOffhand.Contains(item.Value.ItemUICategory.RowId)) {
                    sum += item.Value.LevelItem.RowId;
                    i++;
                }

                sum += item.Value.LevelItem.RowId;
            }

            var avgItemLevel = sum / c;
            var seStr = new SeString(new List<Payload> { new TextPayload($"{avgItemLevel:0000}"), });
            if (!Common.GetNodeById(&previewComponent->UldManager, CustomNodes.Get(this), out AtkTextNode* textNode)) {
                textNode = UiHelper.MakeTextNode(CustomNodes.Get(this));

                textNode->FontSize = 14;
                textNode->AlignmentFontType = 37;
                textNode->FontSize = 16;
                textNode->CharSpacing = 0;
                textNode->LineSpacing = 24;
                textNode->Height = 24;
                textNode->Width = 80;
                textNode->NodeFlags |= NodeFlags.Visible;
                textNode->Y = 0;
                textNode->X = 92;
                textNode->DrawFlags |= 0x1;

                UiHelper.LinkNodeAtEnd(&textNode->AtkResNode, previewComponent);
            }

            if (inaccurate) {
                TooltipManager.AddTooltip(&examineWindow->AtkUnitBase, &textNode->AtkResNode, "Item level is inaccurate due to variable item level items.");
            }

            textNode->TextColor = new ByteColor { R = (byte)(inaccurate ? 0xFF : 0x45), G = (byte)(inaccurate ? 0x83 : 0xB2), B = (byte)(inaccurate ? 0x75 : 0xAE), A = 0xFF };
            textNode->NodeText.SetString(seStr.EncodeWithNullTerminator());

            if (TweakConfig.ShowItemLevelIcon) {
                if (!Common.GetNodeById(&previewComponent->UldManager, CustomNodes.Get(this), out AtkImageNode* iconNode)) {
                    iconNode = UiHelper.MakeImageNode(CustomNodes.Get(this, 1), new UiHelper.PartInfo(176, 138, 24, 24));
                    iconNode->Height = 24;
                    iconNode->Width = 24;
                    iconNode->X = textNode->AtkResNode.X + 2;
                    iconNode->Y = textNode->AtkResNode.Y + 3;
                    iconNode->NodeFlags |= NodeFlags.Visible;
                    iconNode->DrawFlags |= 0x1; // Update
                    iconNode->LoadTexture("ui/uld/Character.tex");
                    UiHelper.LinkNodeAtEnd(&iconNode->AtkResNode, previewComponent);
                }
            }
        } catch (Exception ex) {
            SimpleLog.Log(ex);
            Plugin.Error(this, ex, true);
        }
    }
}
