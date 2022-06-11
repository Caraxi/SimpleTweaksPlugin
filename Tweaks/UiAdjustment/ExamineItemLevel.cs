using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ExamineItemLevel : UiAdjustments.SubTweak {

    public class Config : TweakConfig {
        public bool ShowItemLevelIcon = true;
    }
        
    public Config TweakConfig { get; private set; }

    private readonly uint[] canHaveOffhand = { 2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32 };
    private readonly uint[] ignoreCategory = { 105 };

    public override string Name => "Item Level in Examine";
    public override string Description => "Calculates the item level of other players when examining them.\nRed value means the player is wearing an item that scales to their level and it is showing the max level.";

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox(LocString("ItemLevelIcon", "Show Item Level Icon"), ref TweakConfig.ShowItemLevelIcon);
    };

    private delegate byte CharacterInspectOnRefresh(AtkUnitBase* atkUnitBase, int a2, AtkValue* a3);
    private HookWrapper<CharacterInspectOnRefresh> onExamineRefresh;

    public override void Enable() {
        if (!Ready) return;
        TweakConfig = LoadConfig<Config>() ?? new Config();

        onExamineRefresh ??= Common.Hook<CharacterInspectOnRefresh>("48 89 5C 24 ?? 57 48 83 EC 20 49 8B D8 48 8B F9 4D 85 C0 0F 84 ?? ?? ?? ?? 85 D2", ExamineRefresh);
        onExamineRefresh?.Enable();
        Enabled = true;
    }

    private byte ExamineRefresh(AtkUnitBase* atkUnitBase, int a2, AtkValue* loadingStage) {
        var retVal = onExamineRefresh.Original(atkUnitBase, a2, loadingStage);
        if (loadingStage != null && a2 > 0) {
            if (loadingStage->UInt == 4) {
                ShowItemLevel();
            }
        }
        return retVal;
    }

    private readonly IntPtr allocText = Marshal.AllocHGlobal(512);

    private void ShowItemLevel(bool reset = false) {
        try {
            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
            if (container == null) return;
            var examineWindow = (AddonCharacterInspect*) Common.GetUnitBase("CharacterInspect");
            if (examineWindow == null) return;
            var compInfo = (AtkUldComponentInfo*) examineWindow->PreviewComponent->UldManager.Objects;
            if (compInfo == null || compInfo->ComponentType != ComponentType.Preview) return;
            if (examineWindow->PreviewComponent->UldManager.NodeListCount < 4) return;
            if (reset) {
                examineWindow->PreviewComponent->UldManager.NodeListCount = 4;
                return;
            }

            var nodeList = examineWindow->PreviewComponent->UldManager.NodeList;
            var node = nodeList[3];
            var textNode = UiAdjustments.CloneNode((AtkTextNode*) node);

            var inaccurate = false;
            var sum = 0U;
            var c = 12;
            for (var i = 0; i < 13; i++) {
                if (i == 5) continue;
                var slot = container->GetInventorySlot(i);
                if (slot == null) continue;
                var id = slot->ItemID;
                var item = Service.Data.Excel.GetSheet<Sheets.ExtendedItem>()?.GetRow(id);
                if (item == null) continue;
                if (ignoreCategory.Contains(item.ItemUICategory.Row)) {
                    if (i == 0) c -= 1;
                    c -= 1;
                    continue;
                }

                if ((item.LevelSyncFlag & 2) == 2) inaccurate = true;
                if (i == 0 && !canHaveOffhand.Contains(item.ItemUICategory.Row)) {
                    sum += item.LevelItem.Row;
                    i++;
                }

                sum += item.LevelItem.Row;
            }

            var avgItemLevel = sum / c;

            var seStr = new SeString(new List<Payload>() {new TextPayload($"{avgItemLevel:0000}"),});

            Common.WriteSeString((byte*) allocText, seStr);

            textNode->NodeText.StringPtr = (byte*) allocText;

            textNode->FontSize = 14;
            textNode->AlignmentFontType = 37;
            textNode->FontSize = 16;
            textNode->CharSpacing = 0;
            textNode->LineSpacing = 24;
            textNode->TextColor = new ByteColor() {R = (byte) (inaccurate ? 0xFF : 0x45), G = (byte) (inaccurate ? 0x83 : 0xB2), B = (byte) (inaccurate ? 0x75 : 0xAE), A = 0xFF};

            textNode->AtkResNode.Height = 24;
            textNode->AtkResNode.Width = 80;
            textNode->AtkResNode.Flags |= 0x10;
            textNode->AtkResNode.Y = 0;
            textNode->AtkResNode.X = 92;
            textNode->AtkResNode.Flags_2 |= 0x1;

            var a = UiAdjustments.CopyNodeList(examineWindow->PreviewComponent->UldManager.NodeList, examineWindow->PreviewComponent->UldManager.NodeListCount, (ushort) (examineWindow->PreviewComponent->UldManager.NodeListCount + 5));
            examineWindow->PreviewComponent->UldManager.NodeList = a;
            examineWindow->PreviewComponent->UldManager.NodeList[examineWindow->PreviewComponent->UldManager.NodeListCount++] = (AtkResNode*) textNode;

            if (TweakConfig.ShowItemLevelIcon) {

                var iconNode = (AtkImageNode*) UiAdjustments.CloneNode(examineWindow->AtkUnitBase.UldManager.NodeList[8]);
                iconNode->PartId = 47;

                iconNode->PartsList->Parts[47].Height = 24;
                iconNode->PartsList->Parts[47].Width = 24;
                iconNode->PartsList->Parts[47].U = 176;
                iconNode->PartsList->Parts[47].V = 138;

                iconNode->AtkResNode.Height = 24;
                iconNode->AtkResNode.Width = 24;
                iconNode->AtkResNode.X = textNode->AtkResNode.X + 2;
                iconNode->AtkResNode.Y = textNode->AtkResNode.Y + 3;
                iconNode->AtkResNode.Flags |= 0x10; // Visible
                iconNode->AtkResNode.Flags_2 |= 0x1; // Update

                iconNode->AtkResNode.ParentNode = textNode->AtkResNode.ParentNode;
                iconNode->AtkResNode.PrevSiblingNode = textNode->AtkResNode.PrevSiblingNode;
                if (iconNode->AtkResNode.PrevSiblingNode != null) {
                    iconNode->AtkResNode.PrevSiblingNode->NextSiblingNode = (AtkResNode*) iconNode;
                }

                iconNode->AtkResNode.NextSiblingNode = (AtkResNode*) textNode;

                textNode->AtkResNode.PrevSiblingNode = (AtkResNode*) iconNode;

                examineWindow->PreviewComponent->UldManager.NodeList[examineWindow->PreviewComponent->UldManager.NodeListCount++] = (AtkResNode*) iconNode;

            }
        } catch (Exception ex) {
            SimpleLog.Log(ex);
            Plugin.Error(this, ex, true);
        }
    }

    public override void Disable() {
        SaveConfig(TweakConfig);
        onExamineRefresh?.Disable();
        ShowItemLevel(true);
        Enabled = false;
    }

    public override void Dispose() {
        if (Enabled) Disable();
        Ready = false;
        Enabled = false;
        onExamineRefresh?.Dispose();
        Marshal.FreeHGlobal(allocText);
    }
}