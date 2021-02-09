using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public HighlightInventoryItems.Configs HighlightInventoryItems = new HighlightInventoryItems.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public class HighlightInventoryItems : UiAdjustments.SubTweak
    {
        public class Configs
        {
            public bool HqNqSame = false;
        }

        public Configs Config => PluginConfig.UiAdjustments.HighlightInventoryItems;

        public override string Name => "Highlight Hovered Item in Inventory";
        public override bool Experimental => true;
        private ulong _lastHighlightedItem = 0;
        private Dictionary<InventoryType, List<string>> _containerToAddonMap;
        private List<AtkImageNode> _lastHighlightedSlots;
        
        private enum InventoryAddonType
        {
            Inventory,
            InventoryLarge,
            InventoryExpansion
        }

        public override void DrawConfig(ref bool hasChanged)
        {
            if (Enabled)
            {
                if (ImGui.TreeNode($"{Name}###highlightInventoryConfig"))
                {
                    hasChanged |= ImGui.Checkbox("Highlight NQ with HQ and vice versa.", ref Config.HqNqSame);
                    ImGui.TreePop();
                }
            }
            else
            {
                base.DrawConfig(ref hasChanged);
            }
        }

        public override void Setup()
        {
            Ready = true;
            _containerToAddonMap = new Dictionary<InventoryType, List<string>>
            {
                {InventoryType.Bag0, new() {"InventoryGrid0E"}},
                {InventoryType.Bag1, new() {"InventoryGrid1E"}},
                {InventoryType.Bag2, new() {"InventoryGrid2E"}},
                {InventoryType.Bag3, new() {"InventoryGrid3E"}},
                {InventoryType.SaddleBag0, new() {"InventoryBuddy", "InventoryBuddy2"}},
                {InventoryType.SaddleBag1, new() {"InventoryBuddy", "InventoryBuddy2"}},
                {InventoryType.PremiumSaddleBag0, new() {"InventoryBuddy", "InventoryBuddy2"}},
                {InventoryType.PremiumSaddleBag1, new() {"InventoryBuddy", "InventoryBuddy2"}},
            };
        }

        private unsafe void Debug()
        {
            var module = SimpleTweaksPlugin.Client.UiModule.ItemOrderModule;
            var map = module.Data->RetainerMap->ToDictionary();

            foreach (var pair in map)
            {
                SimpleLog.Log($"{pair.Key:X} : {pair.Value.ToInt64():X}");
            }

            // var result = (*module.Data->RetainerMap)[module.Data->CurrentRetainerID];
            // SimpleLog.Log($"{module.Data->CurrentRetainerID:X} : {result.ToInt64():X}");
        }

        private void HoveredItemChanged(object sender, ulong e)
        {
            // TODO: Check if any inventory is open and return if not?
            if (e == 32132)
            {
                Debug();
            }

            if (_lastHighlightedItem != 0)
            {
                UnhighlightItems();
            }

            _lastHighlightedItem = e;

            if (e == 0)
            {
                return;
            }

            HighlightItems(e);
        }

        private unsafe void HighlightItems(ulong itemId)
        {
            var slotSet = GetSlotsWhereItemIs(itemId);
            _lastHighlightedSlots = new List<AtkImageNode>();

            for (int i = 0; i < slotSet.Length; i++)
            {
                var newSlot = AdjustSlotForUI(slotSet[i].invType, slotSet[i].slot);
                var node = GetSlotIconHighlightNode(slotSet[i].invType, newSlot);
                _lastHighlightedSlots.Add(*node);
                ShowHighlightNode(node);
            }
        }

        private unsafe void UnhighlightItems()
        {
            foreach (var node in _lastHighlightedSlots)
                HideHighlightNode(node);
        }

        private unsafe void ShowHighlightNode(AtkImageNode* highlightNode)
        {
            if (highlightNode == null)
                return;
            highlightNode->AtkResNode.Color.A = 255;
        }

        private void HideHighlightNode(AtkImageNode highlightNode)
        {
            highlightNode.AtkResNode.Color.A = 0;
        }

        private unsafe AtkImageNode* GetSlotIconHighlightNode(InventoryType invType, int slot)
        {
            if (!_containerToAddonMap.TryGetValue(invType, out var unit))
                return null;

            AtkUnitBase* atkbase = Common.GetUnitBase(unit[0]);
            switch (invType)
            {
                case InventoryType.Bag0:
                case InventoryType.Bag1:
                case InventoryType.Bag2:
                case InventoryType.Bag3:
                    return GetSlotIconHighlightNodeBag(atkbase, slot);
                case InventoryType.SaddleBag0:
                case InventoryType.SaddleBag1:
                case InventoryType.PremiumSaddleBag0:
                case InventoryType.PremiumSaddleBag1:
                    return GetSlotIconHighlightNodeSaddlebag(invType, atkbase, slot);
            }

            return null;
        }

        private unsafe AtkImageNode* GetSlotIconHighlightNodeBag(AtkUnitBase* atkBase, int slot)
        {
            if (atkBase == null || atkBase->ULDData.NodeListCount == 0)
                return null;

            int dragDropNodeIndex = 37 - slot % 35;
            SimpleLog.Verbose($"Given slot {slot} dragdrop index is {dragDropNodeIndex}");
            var dragDropNode = SafelyGetComponentNode(atkBase, dragDropNodeIndex);
            SimpleLog.Verbose($"dragDropNode at {(ulong) dragDropNode:X}");
            var iconComponentNode = SafelyGetComponentNode(dragDropNode, 2);
            SimpleLog.Verbose($"iconComponentNode at {(ulong) iconComponentNode:X}");
            var highlightImageNode = SafelyGetComponentNode(iconComponentNode, 7);
            SimpleLog.Verbose($"highlightImageNode at {(ulong) highlightImageNode:X}");
            return (AtkImageNode*) highlightImageNode;
        }
        
        private unsafe AtkImageNode* GetSlotIconHighlightNodeSaddlebag(InventoryType type, AtkUnitBase* atkBase, int slot)
        {
            if (atkBase == null || atkBase->ULDData.NodeListCount == 0)
                return null;

            int nodeIndexModifier = type switch
            {
                InventoryType.SaddleBag0 => 78,
                InventoryType.SaddleBag1 => 42,
                InventoryType.PremiumSaddleBag0 => 78,
                InventoryType.PremiumSaddleBag1 => 42,
            };

            int dragDropNodeIndex = nodeIndexModifier - slot;
            SimpleLog.Verbose($"Given slot {slot} in {type.GetDescription()} dragdrop index is {dragDropNodeIndex}");
            var dragDropNode = SafelyGetComponentNode(atkBase, dragDropNodeIndex);
            SimpleLog.Verbose($"dragDropNode at {(ulong) dragDropNode:X}");
            var iconComponentNode = SafelyGetComponentNode(dragDropNode, 4);
            SimpleLog.Verbose($"iconComponentNode at {(ulong) iconComponentNode:X}");
            var highlightImageNode = SafelyGetComponentNode(iconComponentNode, 7);
            SimpleLog.Verbose($"highlightImageNode at {(ulong) highlightImageNode:X}");
            return (AtkImageNode*) highlightImageNode;
        }
        
        private unsafe (InventoryType invType, int slot)[] GetSlotsWhereItemIs(ulong hoverId)
        {
            var slots = new List<(InventoryType invType, int slot)>();
            var containers = _containerToAddonMap.Keys.ToList();
            for (int containerIndex = 0; containerIndex < containers.Count; containerIndex++)
            {
                var container = Common.GetContainer(containers[containerIndex]);
                for (int slot = 0; slot < container->SlotCount; slot++)
                {
                    if (ConfigAwareItemsEqual(hoverId, container->Items[slot].ItemId, container->Items[slot].IsHQ))
                    {
                        // int newSlot = AdjustSlotForUI(containers[containerIndex], slot);
                        slots.Add((containers[containerIndex], slot));
                    }
                        
                }
                    
            }

            foreach (var data in slots)
            {
                SimpleLog.Verbose($"Item {hoverId} found in {data.invType.GetDescription()} slot {data.slot}");
            }
            
            return slots.ToArray();
        }

        private unsafe int AdjustSlotForUI(InventoryType type, int containerSlotIndex)
        {
            int newSlot = containerSlotIndex;

            var module = SimpleTweaksPlugin.Client.UiModule.ItemOrderModule.Data;
            var inventory = module->PlayerInventory;
            var armoury = module->Armoury;
            var saddlebag = module->Saddlebag;
            var premSaddlebag = module->PremiumSaddlebag;
            var retainer = module->GetCurrentRetainerInventory();
            
            switch (type)
            {
                case InventoryType.Bag0:
                    
                    break;
                case InventoryType.Bag1:
                    break;
                case InventoryType.Bag2:
                    break;
                case InventoryType.Bag3:
                    break;
                case InventoryType.ArmoryOff:
                    break;
                case InventoryType.ArmoryHead:
                    break;
                case InventoryType.ArmoryBody:
                    break;
                case InventoryType.ArmoryHand:
                    break;
                case InventoryType.ArmoryWaist:
                    break;
                case InventoryType.ArmoryLegs:
                    break;
                case InventoryType.ArmoryFeet:
                    break;
                case InventoryType.ArmoryEar:
                    break;
                case InventoryType.ArmoryNeck:
                    break;
                case InventoryType.ArmoryWrist:
                    break;
                case InventoryType.ArmoryRing:
                    break;
                case InventoryType.ArmorySoulCrystal:
                    break;
                case InventoryType.ArmoryMain:
                    break;
                case InventoryType.SaddleBag0:
                    break;
                case InventoryType.SaddleBag1:
                    break;
                case InventoryType.PremiumSaddleBag0:
                    break;
                case InventoryType.PremiumSaddleBag1:
                    break;
                case InventoryType.RetainerBag0:
                    break;
                case InventoryType.RetainerBag1:
                    break;
                case InventoryType.RetainerBag2:
                    break;
                case InventoryType.RetainerBag3:
                    break;
                case InventoryType.RetainerBag4:
                    break;
                case InventoryType.RetainerBag5:
                    break;
                case InventoryType.RetainerBag6:
                    break;
                case InventoryType.FreeCompanyBag0:
                    break;
                case InventoryType.FreeCompanyBag1:
                    break;
                case InventoryType.FreeCompanyBag2:
                    break;
                case InventoryType.FreeCompanyBag3:
                    break;
                case InventoryType.FreeCompanyBag4:
                    break;
                case InventoryType.FreeCompanyBag5:
                    break;
                case InventoryType.FreeCompanyBag6:
                    break;
                case InventoryType.FreeCompanyBag7:
                    break;
                case InventoryType.FreeCompanyBag8:
                    break;
                case InventoryType.FreeCompanyBag9:
                    break;
                case InventoryType.FreeCompanyBag10:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }


            return newSlot;
        }

        private bool ConfigAwareItemsEqual(ulong hoverId, uint invId, bool invIsHq)
        {
            // Equal no matter what
            if (hoverId == invId)
                return true;

            bool hoverIsHq = hoverId > 1000000;
            ulong correctedHover = hoverId > 1000000 ? hoverId - 1000000 : hoverId;
            
            // Ignore Hq status when config is set
            if (Config.HqNqSame && correctedHover == invId)
                return true;
            
            // Do not ignore Hq status when config is set
            if (!Config.HqNqSame && correctedHover == invId && hoverIsHq == invIsHq)
                return true;
            
            return false;
        }
        
        private static unsafe AtkComponentNode* SafelyGetComponentNode(AtkUnitBase* thisNode, int index)
        {
            SimpleLog.Verbose($"Grabbing node at {index} from node list from node @ {(ulong) thisNode:X}");
            if (thisNode == null)
            {
                SimpleLog.Verbose("Initial node was null.");
                return null;
            }

            var count = thisNode->ULDData.NodeListCount;
            if (count == 0 || count - 1 < index)
            {
                SimpleLog.Verbose($"Node's node list had {count}");
                return null;
            }
            
            if (thisNode->ULDData.NodeList == null)
            {
                SimpleLog.Verbose("Node's node list was null!");
                return null;
            }
            
            return (AtkComponentNode*) thisNode->ULDData.NodeList[index];
        }

        private static unsafe AtkComponentNode* SafelyGetComponentNode(AtkComponentNode* thisNode, int index)
        {
            SimpleLog.Verbose($"Grabbing node at {index} from node list from node @ {(ulong) thisNode:X}");
            if (thisNode == null)
            {
                SimpleLog.Verbose("Initial node was null.");
                return null;
            }

            if (thisNode->Component == null)
            {
                SimpleLog.Verbose("Node's component was null.");
                return null;
            }
            var count = thisNode->Component->ULDData.NodeListCount;
            if (count == 0 || count - 1 < index)
            {
                SimpleLog.Verbose($"Node's node list had {count}");
                return null;
            }
            
            if (thisNode->Component->ULDData.NodeList == null)
            {
                SimpleLog.Verbose("Node's node list was null!");
                return null;
            }
            
            return (AtkComponentNode*) thisNode->Component->ULDData.NodeList[index];
        }

        public override void Enable() {
            if (Enabled) return;
            
            PluginInterface.Framework.Gui.HoveredItemChanged += HoveredItemChanged;
            
            Enabled = true;
        }

        public override void Disable()
        {
            if (!Enabled) return;
            
            PluginInterface.Framework.Gui.HoveredItemChanged -= HoveredItemChanged;
            
            Enabled = false;
        }

        public override void Dispose() {
            Enabled = false;
            Ready = false;
            _containerToAddonMap = null;
        }
    }
}