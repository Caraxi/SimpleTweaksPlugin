using System;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Item Level in Examine")]
[TweakDescription("Calculates the item level of other players when examining them.\nRed value means the player is wearing an item that scales to their level and it is showing the max level.")]
[Changelog("1.10.6.0", "Tweak now uses item level provided by the game, improving accuracy.")]
[TweakAutoConfig]
public unsafe class ExamineItemLevel : UiAdjustments.SubTweak {
    public class Config : TweakConfig {
        [TweakConfigOption("Show Item Level Icon")]
        public bool ShowItemLevelIcon = true;
    }

    [TweakConfig] public Config TweakConfig { get; private set; }

    protected override void Disable() => ShowItemLevel();

    [AddonPreDraw("CharacterInspect")]
    private void ShowItemLevel() {
        try {
            var examineWindow = Common.GetUnitBase<AddonCharacterInspect>("CharacterInspect");
            if (examineWindow == null) return;
            var previewComponent = examineWindow->PreviewController.Component;
            var compInfo = (AtkUldComponentInfo*)previewComponent->UldManager.Objects;
            if (compInfo == null || compInfo->ComponentType != ComponentType.Preview) return;
            var errorNode = previewComponent->GetTextNodeById(2);
            var inspect = &UIState.Instance()->Inspect;
            var visible = !(inspect->IsInspectRequested || inspect->EntityId == 0xE000000 || Unloading || errorNode == null || errorNode->IsVisible());

            if (!Common.GetNodeById(&previewComponent->UldManager, CustomNodes.Get(this, "ItemLevel"), out AtkTextNode* textNode) && visible) {
                textNode = UiHelper.MakeTextNode(CustomNodes.Get(this, "ItemLevel"));

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
                textNode->TextColor = new ByteColor {
                    R = 0x45,
                    G = 0xB2,
                    B = 0xAE,
                    A = 0xFF
                };
                textNode->NodeText.SetString($"{inspect->AverageItemLevel:0000}");
                UiHelper.LinkNodeAtEnd(&textNode->AtkResNode, previewComponent);
            }

            if (textNode != null) {
                textNode->TextColor = new ByteColor { R = 0x45, G = 0xB2, B = 0xAE, A = 0xFF };
                textNode->NodeText.SetString($"{inspect->AverageItemLevel:0000}");
                textNode->ToggleVisibility(visible);
            }

            if (!Common.GetNodeById(&previewComponent->UldManager, CustomNodes.Get(this, "ItemLevelIcon"), out AtkImageNode* iconNode) && TweakConfig.ShowItemLevelIcon && visible) {
                iconNode = UiHelper.MakeImageNode(CustomNodes.Get(this, "ItemLevelIcon"), new UiHelper.PartInfo(176, 138, 24, 24));
                iconNode->Height = 24;
                iconNode->Width = 24;
                iconNode->X = textNode->AtkResNode.X + 2;
                iconNode->Y = textNode->AtkResNode.Y + 3;
                iconNode->NodeFlags |= NodeFlags.Visible;
                iconNode->DrawFlags |= 0x1; // Update
                iconNode->LoadTexture("ui/uld/Character.tex");
                UiHelper.LinkNodeAtEnd(&iconNode->AtkResNode, previewComponent);
            }

            if (iconNode != null) {
                iconNode->ToggleVisibility(TweakConfig.ShowItemLevelIcon && visible);
            }
        } catch (Exception ex) {
            SimpleLog.Log(ex);
            Plugin.Error(this, ex);
        }
    }
}
