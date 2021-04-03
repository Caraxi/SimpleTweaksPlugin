using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Hooking;
using FFXIVClientInterface.Client.UI.Misc;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public ExtendedDesynthesisWindow.Configs ExtendedDesynthesisWindow = new ExtendedDesynthesisWindow.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ExtendedDesynthesisWindow : UiAdjustments.SubTweak {

        public class Configs {
            public bool BlockClickOnGearset = false;
        }

        public Configs Config => PluginConfig.UiAdjustments.ExtendedDesynthesisWindow;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Block clicking on gearset items.", ref Config.BlockClickOnGearset);
        };

        public override string Name => "Extended Desynthesis Window";
        public override string Description => "Shows your current desynthesis level and the item's optimal level on the desynthesis item selection window.\nAlso indicates if an item is part of a gear set, optionally preventing selection of gearset items.";

        private const ushort OriginalWidth = 600;
        private const ushort AddedWidth = 110;
        private const ushort NewWidth = OriginalWidth + AddedWidth;

        private delegate IntPtr UpdateItemDelegate(IntPtr a1, ulong index, IntPtr a3, ulong a4);
        private delegate byte UpdateListDelegate(IntPtr a1, IntPtr a2, IntPtr a3);

        private Hook<UpdateItemDelegate> updateItemHook;
        private Hook<UpdateListDelegate> updateListHook;
        
        public override void Enable() {
            updateItemHook ??= new Hook<UpdateItemDelegate>(PluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B 38"), new UpdateItemDelegate(UpdateItemDetour));
            updateItemHook?.Enable();
            updateListHook ??= new Hook<UpdateListDelegate>(PluginInterface.TargetModuleScanner.ScanText("40 53 56 57 48 83 EC 20 48 8B D9 49 8B F0"), new UpdateListDelegate(UpdateListDetour));
            updateListHook?.Enable();
            base.Enable();
        }
        
        private byte UpdateListDetour(IntPtr a1, IntPtr a2, IntPtr a3) {
            var ret = updateListHook.Original(a1, a2, a3);
            Update();
            return ret;
        }

        public override void Disable() {
            updateItemHook?.Disable();
            updateListHook?.Disable();
            Reset();
            base.Disable();
        }

        public override void Dispose() {
            updateItemHook?.Dispose();
            updateListHook?.Dispose();
            base.Dispose();
        }

        private IntPtr UpdateItemDetour(IntPtr a1, ulong a2, IntPtr a3, ulong a4) {
            var ret = updateItemHook.Original(a1, a2, a3, a4); 
            if (desynthRows.ContainsKey(a4)) {
                UpdateRow(desynthRows[a4], a2);
            }
            return ret;
        }

        private void UpdateRow(DesynthRow desynthRow, ulong index) {
            var skillTextNode = desynthRow.SkillTextNode;
            
            if (skillTextNode == null) return;
            var addon = (AddonSalvageItemSelector*) PluginInterface.Framework.Gui.GetUiObjectByName("SalvageItemSelector", 1);
            if (addon != null) {
                if (index > addon->ItemCount) {
                    UiHelper.SetText(skillTextNode, "Error");
                    return;
                }

                var salvageItemAddress = (ulong)addon + 0x268 + (ulong) sizeof(SalvageItem) * index;
                var salvageItem = (SalvageItem*)salvageItemAddress;

                var item = Common.GetInventoryItem(salvageItem->Inventory, salvageItem->Slot);

                var itemData = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(item->ItemId);

                var classJobOffset = 2 * (int)(itemData.ClassJobRepair.Row - 8);
                var desynthLevel = *(ushort*)(Common.PlayerStaticAddress + (0x69A + classJobOffset)) / 100f;
                
                skillTextNode->TextColor = new ByteColor() {
                    A = 0xFF,
                    R = (byte)(desynthLevel > itemData.LevelItem.Row ? 0x00 : 0xCC),
                    G = (byte)(desynthLevel <= itemData.LevelItem.Row ? 0x00 : 0xCC),
                    B = 0x00
                };
                UiHelper.SetText(skillTextNode, $"{desynthLevel:F0}/{itemData.LevelItem.Row}");

                var itemIdWithHQ = item->ItemId;
                if ((item->Flags & ItemFlags.HQ) > 0) itemIdWithHQ += 1000000;
                var gearsetModule = SimpleTweaksPlugin.Client.UiModule.RaptureGearsetModule;
                var itemInGearset = false;
                for (var i = 0; i < 101; i++) {
                    var gearset = &gearsetModule.Gearset[i];
                    if (gearset->ID != i) break;
                    if ((gearset->Flags & GearsetFlag.Exists) != GearsetFlag.Exists) continue;
                    
                    var items = (GearsetItem*) gearset->ItemsData;
                    for (var j = 0; j < 14; j++) {
                        if (items[j].ItemID == itemIdWithHQ) {
                            var name = Encoding.UTF8.GetString(gearset->Name, 0x2F);
                            itemInGearset = true;
                            break;
                        }
                    }

                    if (itemInGearset) break;
                }
                UiHelper.Show(desynthRow.CollisionNode);
                if (itemInGearset) {
                    
                    
                    if (Config.BlockClickOnGearset) {
                        UiHelper.Hide(desynthRow.CollisionNode);
                    }
                    UiHelper.SetText(desynthRow.GearsetWarningNode, $"{(char) SeIconChar.BoxedStar}");
                } else {
                    UiHelper.SetText(desynthRow.GearsetWarningNode, "");
                    
                }
                
            }
        }

        public class DesynthRow {
            public AtkTextNode* SkillTextNode;
            public AtkTextNode* GearsetWarningNode;
            public AtkCollisionNode* CollisionNode;
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

                
                UiHelper.ExpandNodeList(atkUnitBase, 2);
                var newHeaderItem = (AtkTextNode*)UiHelper.CloneNode(nodeList[6]);
                newHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newHeaderItem->NodeText.BufSize);
                UiHelper.SetText(newHeaderItem, "Skill");
                
                newHeaderItem->AtkResNode.X = NewWidth - (AddedWidth + 60);
                newHeaderItem->AtkResNode.Width = AddedWidth;
                newHeaderItem->AtkResNode.ParentNode = nodeList[5];
                newHeaderItem->AtkResNode.NextSiblingNode = nodeList[8];
                nodeList[8]->PrevSiblingNode = (AtkResNode*)newHeaderItem;
                atkUnitBase->ULDData.NodeList[atkUnitBase->ULDData.NodeListCount++] = (AtkResNode*)newHeaderItem;
                
                var gsHeaderItem = (AtkTextNode*)UiHelper.CloneNode(nodeList[6]);
                gsHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)gsHeaderItem->NodeText.BufSize);
                UiHelper.SetText(gsHeaderItem, "Gear\nSet");
                gsHeaderItem->TextFlags |= (byte) TextFlags.MultiLine;
                gsHeaderItem->AtkResNode.X = NewWidth - 80;
                gsHeaderItem->AlignmentFontType = (byte) AlignmentType.Bottom;
                gsHeaderItem->AtkResNode.Width = 30;
                gsHeaderItem->AtkResNode.ParentNode = nodeList[5];
                gsHeaderItem->AtkResNode.NextSiblingNode = (AtkResNode*) newHeaderItem;
                newHeaderItem->AtkResNode.PrevSiblingNode = (AtkResNode*)gsHeaderItem;
                atkUnitBase->ULDData.NodeList[atkUnitBase->ULDData.NodeListCount++] = (AtkResNode*)gsHeaderItem;
                
                for (var i = 2; i < 18; i++) {
                    var listItem = (AtkComponentNode*)listNodeList[i];
                    var listItemNodes = listItem->Component->ULDData.NodeList;
                    UiHelper.SetSize(listItem, NewWidth - 40, null);
                    UiHelper.SetSize(listItemNodes[0], NewWidth - 59, null);
                    UiHelper.SetSize(listItemNodes[1], NewWidth - 59, null);
                    UiHelper.SetSize(listItemNodes[2], NewWidth - 40, null);

                    
                    UiHelper.ExpandNodeList(listItem, 2);
                     
                    var newRowItem = (AtkTextNode*)UiHelper.CloneNode(listItemNodes[3]);
                    newRowItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newRowItem->NodeText.BufSize);
                    UiHelper.SetText(newRowItem, "Error");
                    newRowItem->AtkResNode.X = NewWidth - (AddedWidth + 60);
                    newRowItem->AtkResNode.Width = AddedWidth;
                    newRowItem->AtkResNode.ParentNode = (AtkResNode*)listItem;
                    newRowItem->AtkResNode.NextSiblingNode = listItemNodes[7];
                    newRowItem->AlignmentFontType = (byte)AlignmentType.Center;
                    listItemNodes[7]->PrevSiblingNode = (AtkResNode*)newRowItem;
                    listItem->Component->ULDData.NodeList[listItem->Component->ULDData.NodeListCount++] = (AtkResNode*)newRowItem;
                    
                    var gearsetWarning = (AtkTextNode*)UiHelper.CloneNode(listItemNodes[3]);
                    gearsetWarning->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)gearsetWarning->NodeText.BufSize);
                    UiHelper.SetText(gearsetWarning, "?");
                    gearsetWarning->AtkResNode.X = NewWidth - 80;
                    gearsetWarning->AtkResNode.Width = 30;
                    gearsetWarning->AtkResNode.ParentNode = (AtkResNode*)listItem;
                    gearsetWarning->AtkResNode.NextSiblingNode = (AtkResNode*) newRowItem;
                    gearsetWarning->AlignmentFontType = (byte)AlignmentType.Center;
                    newRowItem->AtkResNode.PrevSiblingNode = (AtkResNode*) gearsetWarning;
                    listItem->Component->ULDData.NodeList[listItem->Component->ULDData.NodeListCount++] = (AtkResNode*)gearsetWarning;
                    
                    desynthRows.Add((ulong) listItem->Component, new DesynthRow() {
                        SkillTextNode = newRowItem,
                        GearsetWarningNode = gearsetWarning,
                        CollisionNode =  (AtkCollisionNode*) listItemNodes[0],
                    });
                }
            }
        }

        public void Reset() {
            var atkUnitBase = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("SalvageItemSelector", 1);
            if (atkUnitBase == null) return;
            UiHelper.Close(atkUnitBase, true);
        }
        
    }
}