using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakAuthor("Enriath")]
[TweakName("Show Glamour in Item Name")]
[TweakDescription("Displays the glamoured item name underneath the real item name.")]
[TweakReleaseVersion("1.9.3.0")]
public unsafe class GlamItemNames : TooltipTweaks.SubTweak {
    private const uint ITEM_TOOLTIP_NAME_NODE_ID = 32;
    private const byte NAME_DEFAULT_FONT_SIZE = 14;

    private const string GLAM_NAME_REPLACEMENT_TEXT = "GlamNameReplacementText";

    private string last = "";

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Service.ClientState.LocalPlayer == null) return;
        if (Service.GameGui.HoveredItem > uint.MaxValue) return;
        AtkUnitBase* unitBase = Common.GetUnitBase("ItemDetail");
        if (unitBase == null) return;

        AtkTextNode* replacementNameNode = Common.GetNodeByID<AtkTextNode>(&unitBase->UldManager, CustomNodes.Get(this, GLAM_NAME_REPLACEMENT_TEXT), NodeType.Text);

        // Hide the replacement widget and show the original widget, so that if we don't need to add a glam name it'll be back to vanilla
        if (replacementNameNode != null) replacementNameNode->AtkResNode.ToggleVisibility(false);

        AtkResNode* vanillaNameNode = unitBase->GetNodeById(ITEM_TOOLTIP_NAME_NODE_ID);
        if (vanillaNameNode == null) return;
        vanillaNameNode->ToggleVisibility(true);

        SeString glamName = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.GlamourName);
        
        // We don't need to do anything the item isn't / cannot be glamoured
        if (glamName == null) return;

        SeString normalName = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ItemName);
        
        normalName.Append(NewLinePayload.Payload);
        normalName.Append(glamName);

        if (replacementNameNode == null) {
            replacementNameNode = CreateReplacementTextNode(vanillaNameNode);
            last = "";
        }

        if (normalName.TextValue != last) {
            last = normalName.TextValue;
            ushort w, h;
            byte fs = NAME_DEFAULT_FONT_SIZE;
            // Shrink the font until we get to 2 lines
            // Also reset the size back to default if another tooltip's name shrank it
            do {
                replacementNameNode->FontSize = fs;
                // Set the text each time we change the font to update the drawn size of the text
                replacementNameNode->SetText(normalName.Encode());
                replacementNameNode->GetTextDrawSize(&w, &h);
                fs -= 1;
            } while(h > 35);
        }

        vanillaNameNode->ToggleVisibility(false); 
        replacementNameNode->AtkResNode.ToggleVisibility(true);
        unitBase->UldManager.UpdateDrawNodeList(); 
    }

    private AtkTextNode* CreateReplacementTextNode(AtkResNode* replacedNode) {
        AtkTextNode* node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        if (node == null) return null;
        node->AtkResNode.Type = NodeType.Text;
        node->AtkResNode.NodeID = CustomNodes.Get(this, GLAM_NAME_REPLACEMENT_TEXT);

        node->AtkResNode.SetWidth(replacedNode->GetWidth());
        node->AtkResNode.SetHeight(replacedNode->GetHeight());
        node->AtkResNode.SetPositionFloat(replacedNode->GetX(), replacedNode->GetY());
        node->FontSize = NAME_DEFAULT_FONT_SIZE;
        node->TextFlags = replacedNode->GetAsAtkTextNode()->TextFlags;
        node->TextFlags2 = replacedNode->GetAsAtkTextNode()->TextFlags2;
        node->AlignmentFontType = replacedNode->GetAsAtkTextNode()->AlignmentFontType;
        node->LineSpacing = replacedNode->GetAsAtkTextNode()->LineSpacing;
        node->SetAlignment(replacedNode->GetAsAtkTextNode()->AlignmentType);

        node->TextColor.R = replacedNode->GetAsAtkTextNode()->TextColor.R;
        node->TextColor.G = replacedNode->GetAsAtkTextNode()->TextColor.G;
        node->TextColor.B = replacedNode->GetAsAtkTextNode()->TextColor.B;
        node->TextColor.A = replacedNode->GetAsAtkTextNode()->TextColor.A;

        node->EdgeColor.R = replacedNode->GetAsAtkTextNode()->EdgeColor.R;
        node->EdgeColor.G = replacedNode->GetAsAtkTextNode()->EdgeColor.G;
        node->EdgeColor.B = replacedNode->GetAsAtkTextNode()->EdgeColor.B;
        node->EdgeColor.A = replacedNode->GetAsAtkTextNode()->EdgeColor.A;

        node->BackgroundColor.R = replacedNode->GetAsAtkTextNode()->BackgroundColor.R;
        node->BackgroundColor.G = replacedNode->GetAsAtkTextNode()->BackgroundColor.G;
        node->BackgroundColor.B = replacedNode->GetAsAtkTextNode()->BackgroundColor.B;
        node->BackgroundColor.A = replacedNode->GetAsAtkTextNode()->BackgroundColor.A;

        node->AtkResNode.ParentNode = replacedNode->ParentNode;
        AtkResNode* prev = replacedNode->PrevSiblingNode;
        replacedNode->PrevSiblingNode = (AtkResNode*)node;
        if (prev != null) prev->NextSiblingNode = (AtkResNode*)node;

        node->AtkResNode.PrevSiblingNode = prev;
        node->AtkResNode.NextSiblingNode = replacedNode;

        return node;
    }

    protected override void Disable() {        
        AtkUnitBase* unitBase = Common.GetUnitBase("ItemDetail");
        if (unitBase != null) {
            AtkResNode* textNode = Common.GetNodeByID(&unitBase->UldManager, CustomNodes.Get(this, GLAM_NAME_REPLACEMENT_TEXT), NodeType.Text);
            if (textNode != null) {
                if (textNode->PrevSiblingNode != null)
                    textNode->PrevSiblingNode->NextSiblingNode = textNode->NextSiblingNode;
                if (textNode->NextSiblingNode != null)
                    textNode->NextSiblingNode->PrevSiblingNode = textNode->PrevSiblingNode;
                unitBase->UldManager.UpdateDrawNodeList();
                textNode->Destroy(true);
            }

            AtkResNode* original = unitBase->GetNodeById(ITEM_TOOLTIP_NAME_NODE_ID);
            if (original != null) {
                original->ToggleVisibility(true);
            }
        }
        
        base.Disable();
    }
}
