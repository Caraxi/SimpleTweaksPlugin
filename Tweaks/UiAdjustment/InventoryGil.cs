using System.Collections.Generic;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class InventoryGil : UiAdjustments.SubTweak {
        public override string Name => "Show Gil in Inventory";
        public override string Description => "Show your current gil in the inventory windows, as it does with retainers.";

        private HookWrapper<Common.AddonOnUpdate> inventoryUpdateHook;
        private HookWrapper<Common.AddonOnUpdate> largeInventoryUpdateHook;
        private HookWrapper<Common.AddonOnUpdate> expansionInventoryUpdateHook;

        public override void Enable() {
            inventoryUpdateHook ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 57 48 83 EC 20 48 89 74 24 ?? 48 8B F9", AfterInventoryUpdate);
            largeInventoryUpdateHook ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 49 8B 58 30", AfterInventoryUpdate);
            expansionInventoryUpdateHook ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 83 B9 ?? ?? ?? ?? ?? 48 8B D9", AfterInventoryUpdate);

            inventoryUpdateHook?.Enable();
            largeInventoryUpdateHook?.Enable();
            expansionInventoryUpdateHook?.Enable();

            Update();
            base.Enable();
        }

        private void AfterInventoryUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
            Update(atkUnitBase);
        }

        private void Update() {
            foreach (var inventoryName in new[] { "Inventory", "InventoryLarge", "InventoryExpansion" }) {
                var atkUnitBase = Common.GetUnitBase(inventoryName);
                if (atkUnitBase == null) continue;
                Update(atkUnitBase);
            }
        }

        private void Update(AtkUnitBase* atkUnitBase) {
            var textNode = atkUnitBase->GetTextNodeById(CustomNodes.InventoryGil);

            if (textNode == null) {
                // Because GetTextNodeById is stupid and doesn't work for added nodes
                for (var n = 0; n < atkUnitBase->UldManager.NodeListCount; n++) {
                    var node = atkUnitBase->UldManager.NodeList[n];
                    if (node == null) continue;
                    if (node->NodeID == CustomNodes.InventoryGil && node->Type == NodeType.Text) {
                        textNode = node->GetAsAtkTextNode();
                        break;
                    }
                }
            }

            if (textNode == null) {
                textNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();

                textNode->AtkResNode.NodeID = CustomNodes.InventoryGil;
                textNode->AtkResNode.Type = NodeType.Text;
                textNode->AtkResNode.SetWidth(200);
                textNode->AtkResNode.SetHeight(21);
                textNode->AtkResNode.SetScale(1, 1);
                textNode->AtkResNode.SetPositionFloat(atkUnitBase->RootNode->Width - 218, atkUnitBase->RootNode->Height - 40);

                textNode->FontSize = 12;
                textNode->AlignmentFontType = 0x05;
                textNode->TextColor.A = 0xFF;
                textNode->TextColor.R = 0xEE;
                textNode->TextColor.G = 0xE1;
                textNode->TextColor.B = 0xC5;
                textNode->EdgeColor.A = 0xFF;
                textNode->EdgeColor.R = 0x00;
                textNode->EdgeColor.G = 0x00;
                textNode->EdgeColor.B = 0x00;

                var lastNode = atkUnitBase->RootNode->ChildNode;
                if (lastNode == null) return;
                while (true) {
                    if (lastNode->PrevSiblingNode == null) break;
                    lastNode = lastNode->PrevSiblingNode;
                }

                lastNode->PrevSiblingNode = (AtkResNode*) textNode;
                textNode->AtkResNode.NextSiblingNode = lastNode;
                textNode->AtkResNode.ParentNode = lastNode->ParentNode;

                atkUnitBase->UldManager.UpdateDrawNodeList();
            }

            var gil = InventoryManager.Instance()->GetItemCountInContainer(1, InventoryType.Currency);


            textNode->SetText(gil.ToString("N0", Culture) + $"{(char) SeIconChar.Gil}");
        }

        private void Cleanup() {
            var destroyList = new List<ulong>();

            foreach (var inventoryName in new[] { "Inventory", "InventoryLarge", "InventoryExpansion" }) {
                var atkUnitBase = Common.GetUnitBase(inventoryName);
                if (atkUnitBase == null) continue;

                var doUpdate = false;
                for (var n = 0; n < atkUnitBase->UldManager.NodeListCount; n++) {
                    var node = atkUnitBase->UldManager.NodeList[n];
                    if (node == null) continue;
                    if (node->NodeID == CustomNodes.InventoryGil) {
                        if (node->ParentNode != null && node->ParentNode->ChildNode == node) node->ParentNode->ChildNode = node->PrevSiblingNode;
                        if (node->PrevSiblingNode != null) node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
                        if (node->NextSiblingNode != null) node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
                        doUpdate = true;
                        destroyList.Add((ulong)node);
                    }
                }

                if (doUpdate) atkUnitBase->UldManager.UpdateDrawNodeList();
            }

            foreach (var a in destroyList) {
                var node = (AtkResNode*)a;
                if (node == null) continue;
                node->Destroy(true);
            }
        }

        public override void Disable() {
            inventoryUpdateHook?.Disable();
            largeInventoryUpdateHook?.Disable();
            expansionInventoryUpdateHook?.Disable();
            Cleanup();
            base.Disable();
        }

        public override void Dispose() {
            inventoryUpdateHook?.Dispose();
            largeInventoryUpdateHook?.Dispose();
            expansionInventoryUpdateHook?.Dispose();
            base.Dispose();
        }
    }
}
