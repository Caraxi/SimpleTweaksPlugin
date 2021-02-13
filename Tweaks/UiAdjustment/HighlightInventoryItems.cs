using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientInterface.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin
{
    public partial class UiAdjustmentsConfig
    {
        public HighlightInventoryItems.Configs HighlightInventoryItems = new HighlightInventoryItems.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    // This entire "tweak" can be reworked to be not absolutely miserable
    public class HighlightInventoryItems : UiAdjustments.SubTweak
    {
        public class Configs
        {
            public bool HqNqSame;
        }

        public Configs Config => PluginConfig.UiAdjustments.HighlightInventoryItems;

        public override string Name => "Highlight Hovered Item in Inventory";
        public override bool Experimental => true;
        private ulong lastHighlightedItem;
        private Dictionary<InventoryType, string> containerToAddonMap;
        private List<IntPtr> lastHighlightedSlots;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => { hasChanged |= ImGui.Checkbox("Treat HQ and NQ variants as the same item.", ref Config.HqNqSame); };

        public override void Setup()
        {
            Ready = true;
            containerToAddonMap = new Dictionary<InventoryType, string>
            {
                {InventoryType.Bag0, "InventoryGrid0E"},
                {InventoryType.Bag1, "InventoryGrid1E"},
                {InventoryType.Bag2, "InventoryGrid2E"},
                {InventoryType.Bag3, "InventoryGrid3E"},
                {InventoryType.SaddleBag0, "InventoryBuddy"},
                {InventoryType.SaddleBag1, "InventoryBuddy"},
                
                // Premium saddlebag has NOT been tested
                {InventoryType.PremiumSaddleBag0, "InventoryBuddy"},
                {InventoryType.PremiumSaddleBag1, "InventoryBuddy"},
                
                // These were here because this dictionary is checked to see if we should highlight items in
                // this container. They are disabled because retainers do not work yet.
                // This structure can be entirely reworked.
                
                // {InventoryType.RetainerBag0, "RetainerGrid0"},
                // {InventoryType.RetainerBag1, "RetainerGrid0"},
                // {InventoryType.RetainerBag2, "RetainerGrid0"},
                // {InventoryType.RetainerBag3, "RetainerGrid0"},
                // {InventoryType.RetainerBag4, "RetainerGrid0"},
                // {InventoryType.RetainerBag5, "RetainerGrid0"},
                // {InventoryType.RetainerBag6, "RetainerGrid0"},
            };
        }

        private void HoveredItemChanged(object sender, ulong e)
        {
            if (lastHighlightedItem != 0)
                UnhighlightItems();

            lastHighlightedItem = e;

            if (e == 0)
                return;

            HighlightItems(e);
        }

        private unsafe void HighlightItems(ulong itemId)
        {
            // Find the slots in container structures where the item is, and get the inventory type, and the slot index
            var slotSet = GetSlotsWhereItemIs(itemId);
            
            // We store all of the slots we highlighted in order to unhighlight them afterwards
            lastHighlightedSlots = new List<IntPtr>();

            for (int i = 0; i < slotSet.Length; i++)
            {
                // We need to adjust our container's slot to the UI slot that the player sees
                var (newContainer, newSlot) = AdjustSlotForUI(slotSet[i].invType, slotSet[i].slot);
                SimpleLog.Debug($"adjusted {slotSet[i].invType} slot {slotSet[i].slot} to container {newContainer} slot {newSlot}");
                
                // Once we have the UI slot, we can traverse the UI to obtain the highlight node
                var node = GetSlotIconHighlightNode(newContainer, newSlot);
                
                // Store a pointer to the node and then highlight it
                lastHighlightedSlots.Add(new IntPtr(node));
                ShowHighlightNode(node);
            }
        }

        private unsafe void UnhighlightItems()
        {
            foreach (var node in lastHighlightedSlots)
                HideHighlightNode((AtkImageNode*) node);
        }

        // TODO: This needs to modify visibility as well. Visibility of which node? not sure
        private unsafe void ShowHighlightNode(AtkImageNode* highlightNode)
        {
            if (highlightNode == null)
                return;
            highlightNode->AtkResNode.Color.A = 255;
        }

        // This does not need to modify visibility, as once you highlight a node once,
        // the highlight node is visibile from then on
        private unsafe void HideHighlightNode(AtkImageNode* highlightNode)
        {
            if (highlightNode == null)
                return;
            highlightNode->AtkResNode.Color.A = 0;
        }

        /// <summary>
        /// Obtains the AtkImageNode* responsible for the item's highlight indicator when hovered over.
        /// </summary>
        /// <param name="invType">The inventory type of the item.</param>
        /// <param name="slot">The slot in the inventory the item is in.</param>
        /// <returns>The image node responsible for the item's highlight indicator.</returns>
        private unsafe AtkImageNode* GetSlotIconHighlightNode(InventoryType invType, int slot)
        {
            if (!containerToAddonMap.TryGetValue(invType, out var unit))
                return null;

            
            AtkUnitBase* atkbase = Common.GetUnitBase(unit);
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
                    // If we can't get the saddlebag's AtkUnitBase, the retainer saddlebag might
                    // be open. This is called "InventoryBuddy2", so try with that one instead
                    if (atkbase == null || !atkbase->IsVisible)
                        atkbase = Common.GetUnitBase(unit + "2");
                    return GetSlotIconHighlightNodeSaddlebag(invType, atkbase, slot);
                case InventoryType.RetainerBag0:
                case InventoryType.RetainerBag1:
                case InventoryType.RetainerBag2:
                case InventoryType.RetainerBag3:
                case InventoryType.RetainerBag4:
                case InventoryType.RetainerBag5:
                case InventoryType.RetainerBag6:
                    // Retainer containers work on a basis of 25 items per container,
                    // while the UI works on 35 items per page.
                    // Get the overall slot using the inventory type, calculate container and slot indices
                    var overallSlot = slot + 25 * ((int) invType - 10000);
                    var containerIndex = overallSlot / 35;
                    var slotIndex = overallSlot % 35;
                    
                    // Get the atk base for the current container grid index, and grab the highlight node for
                    // the container and slot within that
                    var atkBaseString = $"RetainerGrid{containerIndex}";
                    SimpleLog.Debug($"Getting slot icon @ c {containerIndex} s {slotIndex}");
                    atkbase = Common.GetUnitBase(atkBaseString);
                    return GetSlotIconHighlightNodeRetainer(atkbase, slotIndex);
            }

            return null;
        }

        /// <summary>
        /// Traverses the given InventoryGrid's AtkUnitBase to obtain the highlight node for an item at a given slot
        /// within this AtkUnitBase.
        /// </summary>
        /// <param name="atkBase">The AtkUnitBase for the inventory grid containing the item.
        /// MUST BE "InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", or "InventoryGrid3E".</param>
        /// <param name="slot">The slot within this inventory grid to obtain the highlight node for.</param>
        /// <returns>The highlight node for the given slot within this InventoryGrid's AtkUnitBase.</returns>
        private unsafe AtkImageNode* GetSlotIconHighlightNodeBag(AtkUnitBase* atkBase, int slot)
        {
            if (atkBase == null || atkBase->ULDData.NodeListCount == 0)
                return null;

            // This simply calculates the index within the node list given a slot ID
            // Slots go backwards starting at index 37 for slot 0...
            int dragDropNodeIndex = 37 - slot;
            SimpleLog.Verbose($"Given slot {slot} dragdrop index is {dragDropNodeIndex}");
            var dragDropNode = SafelyGetComponentNode(atkBase, dragDropNodeIndex);
            SimpleLog.Verbose($"dragDropNode at {(ulong) dragDropNode:X}");
            var iconComponentNode = SafelyGetComponentNode(dragDropNode, 2);
            SimpleLog.Verbose($"iconComponentNode at {(ulong) iconComponentNode:X}");
            var highlightImageNode = SafelyGetComponentNode(iconComponentNode, 7);
            SimpleLog.Verbose($"highlightImageNode at {(ulong) highlightImageNode:X}");
            return (AtkImageNode*) highlightImageNode;
        }

        /// <summary>
        /// Traverses the given Saddlebag's AtkUnitBase to obtain the highlight node for an item at a given slot
        /// within this AtkUnitBase.
        /// </summary>
        /// <param name="type">The InventoryType of the saddlebag.</param>
        /// <param name="atkBase">The AtkUnitBase for the saddlebag grid containing the item.
        /// MUST BE "InventoryBuddy" or "InventoryBuddy2".</param>
        /// <param name="slot">The slot within this saddlebag grid to obtain the highlight node for.</param>
        /// <returns>The highlight node for the given slot within this saddlebag's AtkUnitBase.</returns>
        private unsafe AtkImageNode* GetSlotIconHighlightNodeSaddlebag(InventoryType type, AtkUnitBase* atkBase, int slot)
        {
            if (atkBase == null || atkBase->ULDData.NodeListCount == 0)
                return null;

            // This provides the modifier for saddlebag slot indices
            // Since InventoryBuddy
            // Slots go backwards starting at index 78 for slot 0, page 1, 77 for slot 1, page 1, etc
            // Index 42 is slot 0, page 2
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

        /// <summary>
        /// Traverses the given RetainerGrid's AtkUnitBase to obtain the highlight node for an item at a given slot
        /// within this AtkUnitBase.
        /// </summary>
        /// <param name="atkBase">The AtkUnitBase for the retainer grid containing the item.
        /// MUST BE "RetainerGrid0", "RetainerGrid1", "RetainerGrid2", "RetainerGrid3", or "RetainerGrid4"</param>
        /// <param name="slot">The slot within this retainer grid to obtain the highlight node for.</param>
        /// <returns>The highlight node for the given slot within this RetainerGrid's AtkUnitBase.</returns>
        private unsafe AtkImageNode* GetSlotIconHighlightNodeRetainer(AtkUnitBase* atkBase, int slot)
        {
            if (atkBase == null || atkBase->ULDData.NodeListCount == 0)
                return null;

            // This simply calculates the index within the node list given a slot ID
            // Slots go backwards starting at index 37 for slot 0...
            int dragDropNodeIndex = 37 - slot;
            SimpleLog.Verbose($"Given slot {slot} dragdrop index is {dragDropNodeIndex}");
            var dragDropNode = SafelyGetComponentNode(atkBase, dragDropNodeIndex);
            SimpleLog.Verbose($"dragDropNode at {(ulong) dragDropNode:X}");
            var iconComponentNode = SafelyGetComponentNode(dragDropNode, 2);
            SimpleLog.Verbose($"iconComponentNode at {(ulong) iconComponentNode:X}");
            var highlightImageNode = SafelyGetComponentNode(iconComponentNode, 7);
            SimpleLog.Verbose($"highlightImageNode at {(ulong) highlightImageNode:X}");
            return (AtkImageNode*) highlightImageNode;
        }

        /// <summary>
        /// Finds the inventoryType and slot index for a given hovered item ID.
        /// </summary>
        /// <param name="hoverId">The item ID of a hovered item. This is important as the
        /// function assumes input HQ item IDs are greater than 1,000,000.</param>
        /// <returns>An array of tuples containing the inventoryType and slot index of each
        /// occurrence of the given item in the player's inventories.</returns>
        private unsafe (InventoryType invType, int slot)[] GetSlotsWhereItemIs(ulong hoverId)
        {
            var slots = new List<(InventoryType invType, int slot)>();
            var containers = containerToAddonMap.Keys.ToList();
            for (int containerIndex = 0; containerIndex < containers.Count; containerIndex++)
            {
                var container = Common.GetContainer(containers[containerIndex]);
                for (int slot = 0; slot < container->SlotCount; slot++)
                {
                    if (ConfigAwareItemsEqual(hoverId, container->Items[slot].ItemId, container->Items[slot].IsHQ))
                    {
                        slots.Add((containers[containerIndex], slot));
                    }
                }
            }

            foreach (var data in slots)
            {
                SimpleLog.Debug($"Item {hoverId} found in {data.invType.GetDescription()} slot {data.slot}");
            }

            return slots.ToArray();
        }

        /// <summary>
        /// Attempts to remap a container slot to a UI slot given the InventoryType and the slot index using the
        /// item order module.
        /// </summary>
        /// <param name="type">The InventoryType where the item is present.</param>
        /// <param name="slotIndex">The slot where the item is present.</param>
        /// <returns>The adjusted InventoryType and slotIndex describing the item's location in the UI.</returns>
        private unsafe (InventoryType type, int slotIndex) AdjustSlotForUI(InventoryType type, int slotIndex)
        {
            int newSlot = slotIndex;
            InventoryType newType = type;
            
            var module = SimpleTweaksPlugin.Client.UiModule.ItemOrderModule.Data;
            var inventory = module->PlayerInventory;
            var saddlebag = module->Saddlebag;
            var premSaddlebag = module->PremiumSaddlebag;
            var retainer = module->GetCurrentRetainerInventory();

            // Supported, pre-set values to iterate over. These could be moved to setup or reworked entirely
            var (container, containerIndex, count, itemsPerPage) = type switch
            {
                InventoryType.Bag0 => (new IntPtr(inventory->ItemOrders), 0, 4, inventory->SlotPerContainer),
                InventoryType.Bag1 => (new IntPtr(inventory->ItemOrders), 1, 4, inventory->SlotPerContainer),
                InventoryType.Bag2 => (new IntPtr(inventory->ItemOrders), 2, 4, inventory->SlotPerContainer),
                InventoryType.Bag3 => (new IntPtr(inventory->ItemOrders), 3, 4, inventory->SlotPerContainer),
                InventoryType.SaddleBag0 => (new IntPtr(saddlebag->ItemOrders), 0, 2, saddlebag->SlotPerContainer),
                InventoryType.SaddleBag1 => (new IntPtr(saddlebag->ItemOrders), 1, 2, saddlebag->SlotPerContainer),
                InventoryType.PremiumSaddleBag0 => (new IntPtr(premSaddlebag->ItemOrders), 0, 2, premSaddlebag->SlotPerContainer),
                InventoryType.PremiumSaddleBag1 => (new IntPtr(premSaddlebag->ItemOrders), 1, 2, premSaddlebag->SlotPerContainer),
                InventoryType.RetainerBag0 => (new IntPtr(retainer->ItemOrders), 0, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag1 => (new IntPtr(retainer->ItemOrders), 1, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag2 => (new IntPtr(retainer->ItemOrders), 2, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag3 => (new IntPtr(retainer->ItemOrders), 3, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag4 => (new IntPtr(retainer->ItemOrders), 4, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag5 => (new IntPtr(retainer->ItemOrders), 5, 7, retainer->SlotPerContainer),
                InventoryType.RetainerBag6 => (new IntPtr(retainer->ItemOrders), 6, 7, retainer->SlotPerContainer),
            };

            // Find the overall slot index within this container, making sure to search for the index within the
            // order list that has the current container index and the current slot index
            newSlot = FindItemAtSlot((ItemOrder*) container, (int) itemsPerPage * count, containerIndex, slotIndex);
            
            // newSlot is a single value describing container and slot indices, so:
            int newContainerId = (int) (newSlot / itemsPerPage);
            int newNewSlot = newSlot % (int) itemsPerPage;
            
            // newSlot is -1 if something messed up and we couldn't find the given container and slot 
            if (newSlot != -1)
            {
                // Retainers need special handling because their container pages and UI pages are not the same size
                if (IsRetainerType(type))
                {
                    // I have no idea here. Retainers currently do not work and I cannot figure it out
                    newType = GetEnumFromTypeAndIndex(type, newContainerId);
                    SimpleLog.Debug("---");
                    SimpleLog.Debug($"RetainerItem: calculated containerId {containerIndex} for FindItemAtSlot");
                    SimpleLog.Debug($"RetainerItem: c {type} s {slotIndex} -> {newSlot} overall");
                    SimpleLog.Debug($"RetainerItem: -> nc {newType} ns {newNewSlot}");
                    SimpleLog.Debug("---");
                }
                return (newType, newNewSlot);
            }
            return (0, (short) newSlot);
        }

        /// <summary>
        /// Linear search for the ItemOrder index containing the given containerId and slotId.
        /// </summary>
        /// <param name="orders">A pointer to the ItemOrder array to iterate over.</param>
        /// <param name="orderLen">The length of the ItemOrder array.</param>
        /// <param name="containerId">The containerId to search for.</param>
        /// <param name="slotId">The slotId to search for.</param>
        /// <returns></returns>
        private unsafe int FindItemAtSlot(ItemOrder* orders, int orderLen, int containerId, int slotId)
        {
            for (int i = 0; i < orderLen; i++)
            {
                if (orders[i].ContainerIndex == containerId && orders[i].SlotIndex == slotId)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Utilizes the HighlightInventoryItems config to check if items are equal.
        /// </summary>
        /// <param name="hoverId">The ID provided by the hovered item.</param>
        /// <param name="invId">The ID of an item in a container.</param>
        /// <param name="invIsHq">Whether or not the inventory item is HQ.</param>
        /// <returns>
        /// True if the items are both HQ or both NQ, regardless of configuration.
        /// True if the items are the same item with different HQ/NQ, if HqNqSame is true.
        /// </returns>
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

        /// <summary>
        /// Safely (with null checks) obtains an AtkComponentNode* from the node list of an AtkUnitBase* at the given index.
        /// </summary>
        /// <param name="thisNode">A pointer to the AtkUnitBase whose node list should be used.</param>
        /// <param name="index">The index of the desired node in the node list.</param>
        /// <returns>The desired AtkComponentNode if the index is within the bounds of the node list and
        /// the node list at the given index is not null, and null otherwise.</returns>
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

        /// <summary>
        /// Safely (with null checks) obtains an AtkComponentNode* from the node list of an AtkComponentNode* at the given index.
        /// </summary>
        /// <param name="thisNode">A pointer to the AtkComponentNode whose node list should be used.</param>
        /// <param name="index">The index of the desired node in the node list.</param>
        /// <returns>The desired AtkComponentNode if the index is within the bounds of the node list and
        /// the node list at the given index is not null, and null otherwise.</returns>
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
        
        private bool IsBagType(InventoryType type)
        {
            return type switch
            {
                InventoryType.Bag0 => true,
                InventoryType.Bag1 => true,
                InventoryType.Bag2 => true,
                InventoryType.Bag3 => true,
                _ => false
            };
        }

        private bool IsSaddlebagType(InventoryType type)
        {
            return type switch
            {
                InventoryType.SaddleBag0 => true,
                InventoryType.SaddleBag1 => true,
                _ => false
            };
        }

        private bool IsPremiumSaddlebagType(InventoryType type)
        {
            return type switch
            {
                InventoryType.PremiumSaddleBag0 => true,
                InventoryType.PremiumSaddleBag1 => true,
                _ => false
            };
        }

        private bool IsRetainerType(InventoryType type)
        {
            return type switch
            {
                InventoryType.RetainerBag0 => true,
                InventoryType.RetainerBag1 => true,
                InventoryType.RetainerBag2 => true,
                InventoryType.RetainerBag3 => true,
                InventoryType.RetainerBag4 => true,
                InventoryType.RetainerBag5 => true,
                InventoryType.RetainerBag6 => true,
                _ => false
            };
        }

        /// <summary>
        /// Remaps an Inventory type and new container index to a new InventoryType
        /// </summary>
        /// <param name="oldType">The original InventoryType</param>
        /// <param name="index">The new container index</param>
        /// <returns>The new InventoryType matching the old InventoryType's container type, adjusted for the new container index.</returns>
        private InventoryType GetEnumFromTypeAndIndex(InventoryType oldType, int index)
        {
            if (IsBagType(oldType))
            {
                return index switch
                {
                    0 => InventoryType.Bag0,
                    1 => InventoryType.Bag1,
                    2 => InventoryType.Bag2,
                    3 => InventoryType.Bag3,
                };
            }
            
            if (IsSaddlebagType(oldType))
            {
                return index switch
                {
                    0 => InventoryType.SaddleBag0,
                    1 => InventoryType.SaddleBag1,
                };
            }
            
            if (IsPremiumSaddlebagType(oldType))
            {
                return index switch
                {
                    0 => InventoryType.PremiumSaddleBag0,
                    1 => InventoryType.PremiumSaddleBag1,
                };
            }
            
            if (IsRetainerType(oldType))
            {
                return index switch
                {
                    0 => InventoryType.RetainerBag0,
                    1 => InventoryType.RetainerBag1,
                    2 => InventoryType.RetainerBag2,
                    3 => InventoryType.RetainerBag3,
                    4 => InventoryType.RetainerBag4,
                    5 => InventoryType.RetainerBag5,
                    6 => InventoryType.RetainerBag6,
                };
            }

            return oldType;
        }

        public override void Enable()
        {
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

        public override void Dispose()
        {
            Enabled = false;
            Ready = false;
            containerToAddonMap = null;
        }
    }
}