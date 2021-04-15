using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public ExamineItemLevel.Config ExamineItemLevel = new ExamineItemLevel.Config();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {

    public class ExamineItemLevel : UiAdjustments.SubTweak {

        public class Config {
            public bool ShowItemLevelIcon = true;
        }
        
        private delegate IntPtr ExamineUpdated(IntPtr a1, int a2, byte a3);
        private Hook<ExamineUpdated> examinedUpdatedHook;
        private IntPtr examineUpdatedAddress;


        private readonly uint[] canHaveOffhand = { 2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32 };
        private readonly uint[] ignoreCategory = { 105 };

        public override string Name => "Item Level in Examine";
        public override string Description => "Calculates the item level of other players when examining them.\nRed value means the player is wearing an item that scales to their level and it is showing the max level.";

        private IntPtr examineIsValidPtr = IntPtr.Zero;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Show Item Level Icon", ref PluginConfig.UiAdjustments.ExamineItemLevel.ShowItemLevelIcon);
        };

        public override void Setup() {
            examineIsValidPtr = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 C7 43 ?? ?? ?? ?? ??");

            examineUpdatedAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 89 04 9F");

            SimpleLog.Log($"ExamineIsValidPtr: {examineIsValidPtr.ToInt64():X}");
            Ready = true;
        }

        public override void Enable() {
            if (!Ready) return;

            examinedUpdatedHook ??= new Hook<ExamineUpdated>(examineUpdatedAddress, new ExamineUpdated(ExamineUpdatedDetour));
            examinedUpdatedHook?.Enable();

            Enabled = true;
        }

        private IntPtr ExamineUpdatedDetour(IntPtr a1, int a2, byte a3) {
            var r = examinedUpdatedHook.Original(a1, a2, a3);
            ShowItemLevel();
            return r;
        }

        private readonly IntPtr allocText = Marshal.AllocHGlobal(512);

        private unsafe void ShowItemLevel(bool reset = false) {
            SimpleLog.Log("Add ItemLevel to CharacterInspect");
            try {
                if (examineIsValidPtr == IntPtr.Zero) return;
                if (*(byte*) (examineIsValidPtr + 0x2A8) == 0) return;
                var container = Common.GetContainer(InventoryType.Examine);
                if (container == null) return;
                var examineWindow = (AddonCharacterInspect*) Common.GetUnitBase("CharacterInspect");
                if (examineWindow == null) return;
                var compInfo = (ULDComponentInfo*) examineWindow->PreviewComponent->UldManager.Objects;
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
                var c = 13;
                for (var i = 0; i < 13; i++) {
                    var slot = Common.GetContainerItem(container, i);
                    if (slot == null) continue;
                    var id = slot->ItemId;
                    var item = PluginInterface.Data.Excel.GetSheet<Sheets.ExtendedItem>().GetRow(id);
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

                Plugin.Common.WriteSeString((byte*) allocText, seStr);

                textNode->NodeText.StringPtr = (byte*) allocText;

                textNode->FontSize = 14;
                textNode->AlignmentFontType = 37;
                textNode->FontSize = 16;
                textNode->CharSpacing = 0;
                textNode->LineSpacing = 24;
                textNode->TextColor = new ByteColor() {R = (byte) (inaccurate ? 0xFF : 0x45), G = (byte) (inaccurate ? (byte) 0x83 : (byte) 0xB2), B = (byte) (inaccurate ? 0x75 : 0xAE), A = 0xFF};

                textNode->AtkResNode.Height = 24;
                textNode->AtkResNode.Width = 80;
                textNode->AtkResNode.Flags |= 0x10;
                textNode->AtkResNode.Y = 0;
                textNode->AtkResNode.X = 92;
                textNode->AtkResNode.Flags_2 |= 0x1;

                var a = UiAdjustments.CopyNodeList(examineWindow->PreviewComponent->UldManager.NodeList, examineWindow->PreviewComponent->UldManager.NodeListCount, (ushort) (examineWindow->PreviewComponent->UldManager.NodeListCount + 5));
                examineWindow->PreviewComponent->UldManager.NodeList = a;
                examineWindow->PreviewComponent->UldManager.NodeList[examineWindow->PreviewComponent->UldManager.NodeListCount++] = (AtkResNode*) textNode;

                if (PluginConfig.UiAdjustments.ExamineItemLevel.ShowItemLevelIcon) {

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
            examinedUpdatedHook?.Disable();
            ShowItemLevel(true);
            Enabled = false;
        }

        public override void Dispose() {
            if (Enabled) Disable();
            Ready = false;
            Enabled = false;
            examinedUpdatedHook?.Dispose();
            Marshal.FreeHGlobal(allocText);
        }
    }
}
