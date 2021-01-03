using System;
using System.Collections.Generic;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using FFXIVClientStructs;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.GameStructs;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ExtendedDesynthesisWindow : UiAdjustments.SubTweak {
        public override string Name => "Extended Desynthesis Window";

        private const ushort OriginalWidth = 600;
        private const ushort AddedWidth = 90;
        private const ushort NewWidth = OriginalWidth + AddedWidth;

        private delegate IntPtr SubDelegate(IntPtr a1, ulong index, IntPtr a3, ulong a4);

        private Hook<SubDelegate> subHook;

        public override void Enable() {
            subHook ??= new Hook<SubDelegate>(PluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B 38"), new SubDelegate(SubDetour));
            subHook?.Enable();
            base.Enable();
        }

        public override void Disable() {
            subHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            subHook.Dispose();
            base.Dispose();
        }

        private IntPtr SubDetour(IntPtr a1, ulong a2, IntPtr a3, ulong a4) {
            var ret = subHook.Original(a1, a2, a3, a4);
            if (!desynthRows.ContainsKey(a4)) {
                Update();
            }

            if (desynthRows.ContainsKey(a4)) {
                UpdateRow(desynthRows[a4].SkillTextNode, a2);
            }
            
            return ret;
        }

        private void UpdateRow(AtkTextNode* skillTextNode, ulong index) {
            if (skillTextNode == null) return;
            var addon = (AddonSalvageItemSelector*) PluginInterface.Framework.Gui.GetUiObjectByName("SalvageItemSelector", 1);
            if (addon != null) {
                if (index > addon->ItemCount) {
                    UiHelper.SetText(skillTextNode, "???");
                    return;
                }

                var salvageItemAddress = (ulong)addon + 0x268 + (ulong) sizeof(SalvageItem) * index;
                var salvageItem = (SalvageItem*)salvageItemAddress;

                var item = Common.GetInventoryItem(salvageItem->Container, salvageItem->Slot);
                SimpleLog.Log($"{index}: {(ulong)item:X}");

                var itemData = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(item->ItemId);

                var classJobOffset = 2 * (int)(itemData.ClassJobRepair.Row - 8);
                var desynthLevel = *(ushort*)(Common.PlayerStaticAddress + (0x69A + classJobOffset)) / 100f;

                skillTextNode->TextColor = new FFXIVByteColor() {
                    A = 0xFF,
                    R = (byte)(desynthLevel > itemData.LevelItem.Row ? 0x00 : 0xCC),
                    G = (byte)(desynthLevel <= itemData.LevelItem.Row ? 0x00 : 0xCC),
                    B = 0x00
                };
                UiHelper.SetText(skillTextNode, $"{desynthLevel:F0}/{itemData.LevelItem.Row}");
            }
        }

        public class DesynthRow {
            public AtkTextNode* SkillTextNode;
        }

        private Dictionary<ulong, DesynthRow> desynthRows = new Dictionary<ulong, DesynthRow>();

        private void Update() {

            var atkUnitBase = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("SalvageItemSelector", 1);

            if (atkUnitBase == null) return;
            if ((atkUnitBase->Flags & 0x20) != 0x20) return;

            var nodeList = atkUnitBase->ULDData.NodeList;
            var windowNode = (AtkComponentNode*)atkUnitBase->ULDData.NodeList[1];

            if (windowNode->AtkResNode.Width == 600) {
                desynthRows.Clear();
                UiHelper.SetWindowSize(windowNode, NewWidth, null);
                UiHelper.SetSize(nodeList[0], NewWidth, null);
                UiHelper.SetSize(nodeList[3], NewWidth - 32, null);
                UiHelper.SetSize(nodeList[4], NewWidth - 25, null);
                UiHelper.SetSize(nodeList[5], NewWidth - 32, null);
                UiHelper.SetSize(nodeList[2], nodeList[2]->Width + AddedWidth, null);
                var listComponent = (AtkComponentNode*)atkUnitBase->ULDData.NodeList[3];
                var listNodeList = listComponent->Component->ULDData.NodeList;
                UiHelper.SetSize(listNodeList[0], NewWidth - 32, null);
                UiHelper.SetPosition(listNodeList[1], NewWidth - 40, null);

                var newHeaderItem = (AtkTextNode*)UiHelper.CloneNode(nodeList[6]);
                newHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newHeaderItem->NodeText.BufSize);
                UiHelper.SetText(newHeaderItem, "Skill");
                UiHelper.ExpandNodeList(atkUnitBase, 1);
                newHeaderItem->AtkResNode.X = NewWidth - (AddedWidth + 48);
                newHeaderItem->AtkResNode.Width = AddedWidth;
                newHeaderItem->AtkResNode.ParentNode = nodeList[5];
                newHeaderItem->AtkResNode.NextSiblingNode = nodeList[8];
                nodeList[8]->PrevSiblingNode = (AtkResNode*)newHeaderItem;
                atkUnitBase->ULDData.NodeList[atkUnitBase->ULDData.NodeListCount++] = (AtkResNode*)newHeaderItem;
                
                for (var i = 2; i < 18; i++) {
                    var listItem = (AtkComponentNode*)listNodeList[i];
                    var listItemNodes = listItem->Component->ULDData.NodeList;
                    UiHelper.SetSize(listItem, NewWidth - 40, null);
                    UiHelper.SetSize(listItemNodes[0], NewWidth - 59, null);
                    UiHelper.SetSize(listItemNodes[1], NewWidth - 59, null);
                    UiHelper.SetSize(listItemNodes[2], NewWidth - 40, null);


                    var newRowItem = (AtkTextNode*)UiHelper.CloneNode(listItemNodes[3]);
                    newRowItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newRowItem->NodeText.BufSize);
                    UiHelper.SetText(newRowItem, "???");
                    UiHelper.ExpandNodeList(listItem, 1);
                    newRowItem->AtkResNode.X = NewWidth - (AddedWidth + 48);
                    newRowItem->AtkResNode.Width = AddedWidth;
                    newRowItem->AtkResNode.ParentNode = (AtkResNode*)listItem;
                    newRowItem->AtkResNode.NextSiblingNode = listItemNodes[7];
                    newRowItem->AlignmentFontType = (byte)AlignmentType.Center;
                    listItemNodes[7]->PrevSiblingNode = (AtkResNode*)newRowItem;
                    listItem->Component->ULDData.NodeList[listItem->Component->ULDData.NodeListCount++] = (AtkResNode*)newRowItem;

                    desynthRows.Add((ulong) listItem->Component, new DesynthRow() {
                        SkillTextNode = newRowItem
                    });
                }
            }
        }
    }
}