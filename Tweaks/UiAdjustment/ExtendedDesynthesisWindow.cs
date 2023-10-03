using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Exd;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;
using Vector2 = System.Numerics.Vector2;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ExtendedDesynthesisWindow : UiAdjustments.SubTweak {

        public class Configs : TweakConfig {
            
            public bool BlockClickOnGearset;
            public bool YellowForSkillGain = true;
            public bool Delta;
            
            public bool ShowAll;
            public bool ShowAllExcludeNoSkill;
            public bool ShowAllExcludeGearset;
            public bool ShowAllDefault;
            public bool ShowAllExcludeArmoury;
            public int[] ShowAllSorting;
        }

        public Configs Config { get; private set; }
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
            ImGui.Checkbox(LocString("BlockClickOnGearset", "Block clicking on gearset items."), ref Config.BlockClickOnGearset);
            ImGui.Checkbox(LocString("YellowForSkillGain", "Highlight potential skill gains (Yellow)"), ref Config.YellowForSkillGain);
            ImGui.Checkbox(LocString("DesynthesisDelta", "Show desynthesis delta"), ref Config.Delta);

            if (ImGui.Checkbox(LocString("ShowAll", "Add option to show all items"), ref Config.ShowAll)) Common.CloseAddon("SalvageItemSelector");

            if (Config.ShowAll) {
                ImGui.Indent();
                if (ImGui.Checkbox(LocString("ShowAllDefault", "Make 'All Items' the default option."), ref Config.ShowAllDefault)) Common.CloseAddon("SalvageItemSelector");
                ImGui.TextDisabled(LocString("LimitNote", "Note: Only 140 items can be displayed at once, anything more will be cut off."));
                ImGui.Text(LocString("ExcludeHeader", "Exlude from the 'All Items' category:"));
                ImGui.Indent();
                var u = ImGui.Checkbox(LocString("ExcludeNoSkill","Items that give no desynthesis levels."), ref Config.ShowAllExcludeNoSkill);
                u |= ImGui.Checkbox(LocString("ExcludeGearSet", "Items in gear sets."), ref Config.ShowAllExcludeGearset);
                u |= ImGui.Checkbox(LocString("ExcludeArmoury", "Items in armoury chest."), ref Config.ShowAllExcludeArmoury);
                ImGui.Unindent();
                
                ImGui.Dummy(new Vector2(5));
                
                ImGui.Text("Sorting:");
                ImGui.Indent();
                if (Config.ShowAllSorting == null) DefaultSorting();
                var displayedOptions = new List<ItemListSortMethod>();
                if (ImGui.BeginTable("itemListSortingTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {

                    var descSize = ImGui.CalcTextSize("Desc").X;
                    
                    ImGui.TableSetupColumn("##controlButtons", ImGuiTableColumnFlags.WidthFixed, 65 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn("Desc", ImGuiTableColumnFlags.WidthFixed, descSize);
                    ImGui.TableSetupColumn("Sorting Method", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();

                    var removeIndex = -1;
                    
                    for (var i = 0; i < Config.ShowAllSorting.Length; i++) {
                        
                        var key = Config.ShowAllSorting[i];
                        var desc = false;
                        if (key < 0) {
                            key = -key;
                            desc = true;
                        }
                        if (key == 0 || key > SortMethods.Count) continue;
                        var sortMethod = SortMethods[key - 1];
                        displayedOptions.Add(sortMethod);
                        ImGui.PushID($"itemListSortRow_{key}");
                        ImGui.TableNextColumn();

                        var btnSize = new Vector2(ImGui.GetTextLineHeightWithSpacing());
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetIO().FontGlobalScale));
                        var tooltip = string.Empty;
                        if (ImGui.Button($"{(char) FontAwesomeIcon.Minus}", btnSize)) {
                            removeIndex = i;
                            u = true;
                        }
                        if (ImGui.IsItemHovered()) tooltip = "Remove From Sorting";
                        
                        ImGui.SameLine();
                        if (i != 0) {
                            if (ImGui.Button($"{(char) FontAwesomeIcon.ArrowUp}", btnSize)) {
                                (Config.ShowAllSorting[i - 1], Config.ShowAllSorting[i]) = (Config.ShowAllSorting[i], Config.ShowAllSorting[i - 1]);
                                u = true;
                            }
                            if (ImGui.IsItemHovered()) tooltip = "Move Up";
                        } else {
                            ImGui.Dummy(btnSize);
                        }
                        
                        ImGui.SameLine();
                        if (i != Config.ShowAllSorting.Length - 1) {
                            if (ImGui.Button($"{(char) FontAwesomeIcon.ArrowDown}", btnSize)) {
                                (Config.ShowAllSorting[i + 1], Config.ShowAllSorting[i]) = (Config.ShowAllSorting[i], Config.ShowAllSorting[i + 1]);
                                u = true;
                            }
                            if (ImGui.IsItemHovered()) tooltip = "Move Down";
                        } else {
                            ImGui.Dummy(btnSize);
                        }
                        
                        ImGui.PopStyleVar();
                        ImGui.PopFont();
                        if (!string.IsNullOrEmpty(tooltip)) {
                            ImGui.SetTooltip(tooltip);
                        }
                        
                        ImGui.TableNextColumn();
                        
                        if (ImGui.Checkbox($"##toggleItemListSortDescending_{key}", ref desc)) {
                            Config.ShowAllSorting[i] = desc ? -key : key;
                            u = true;
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text(sortMethod.Name);
                        if (desc) {
                            ImGui.SameLine();
                            ImGui.TextDisabled("Descending");
                        }
                        ImGui.PopID();
                    }

                    if (removeIndex >= 0) {
                        var tempArr = Config.ShowAllSorting.ToList();
                        tempArr.RemoveAt(removeIndex);
                        Config.ShowAllSorting = tempArr.ToArray();
                        u = true;
                    }
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextRow();
                    ImGui.TableNextRow();
                    for (var i = 0; i < SortMethods.Count; i++) {
                        var method = SortMethods[i];
                        if (displayedOptions.Contains(method)) continue;
                        ImGui.PushID($"disabledSortMethood_{method.Name}");
                        ImGui.TableNextColumn();
                        var btnSize = new Vector2(ImGui.GetTextLineHeightWithSpacing());
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button($"{(char) FontAwesomeIcon.Plus}", btnSize)) {
                            var tempArr = Config.ShowAllSorting.ToList();
                            tempArr.Add(method.DefaultDescending ? -(i+1) : i+1);
                            Config.ShowAllSorting = tempArr.ToArray();
                            u = true;
                        }
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add to sorting.");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.TextDisabled($"{method.Name}");
                        ImGui.PopID();
                    }
                    
                    ImGui.EndTable();
                }
                
                ImGui.Unindent();
                ImGui.Unindent();
                if (u) {
                    SetupAllItemList(AgentSalvage.Instance());
                }
            }
        };

        private class ItemListSortMethod {
            public string Name { get; init; }
            public bool DefaultDescending { get; init; }
            public Func<ItemEntry, object> ApplySorting { get; init; }

        }
        
        private List<ItemListSortMethod> SortMethods = new() {
            new() { Name = "Desynthesis Class", ApplySorting = (i) => i.Item?.ClassJobRepair?.Row ?? 0},
            new() { Name = "Item Level", ApplySorting = (i) => i.Item?.LevelItem?.Row ?? 0, DefaultDescending = true},
            new() { Name = "Item Name", ApplySorting = (i) => i.Item?.Name?.RawString ?? string.Empty},
        };

        public override string Name => "Extended Desynthesis Window";
        public override string Description => "Shows your current desynthesis level and the item's optimal level on the desynthesis item selection window.\nAlso indicates if an item is part of a gear set, optionally preventing selection of gearset items.";

        private const ushort OriginalWidth = 600;
        private const ushort AddedWidth = 110;
        private const ushort NewWidth = OriginalWidth + AddedWidth;

        private delegate IntPtr UpdateItemDelegate(IntPtr a1, ulong index, IntPtr a3, ulong a4);
        private HookWrapper<UpdateItemDelegate> updateItemHook;

        private delegate void* SetupDropDownList(AtkComponentList* a1, ushort a2, byte** a3, byte a4);
        private HookWrapper<SetupDropDownList> setupDropDownList;
        
        private delegate byte PopulateItemList(AgentSalvage* agentSalvage);

        private HookWrapper<PopulateItemList> populateHook;

        private delegate void* Callback(AtkUnitBase* atkUnitBase, int count, AtkValue* values, void* a4);
        private HookWrapper<Callback> callbackHook;

        private static readonly ByteColor Red = new() { A = 0xFF, R = 0xEE, G = 0x44, B = 0x44 };
        private static readonly ByteColor Green = new() { A = 0xFF, R = 0x00, G = 0xCC, B = 0x00 };
        private static readonly ByteColor Yellow = new() { A = 0xFF, R = 0xCC, G = 0xCC, B = 0x00 };

        private uint maxDesynthLevel = 590;

        private IntPtr allItemsString;
        private IntPtr alternateList;
        

        public override void Setup() {
            foreach (var i in Service.Data.Excel.GetSheet<Item>()) {
                if (i.Desynth > 0 && i.LevelItem.Row > maxDesynthLevel) maxDesynthLevel = i.LevelItem.Row;
            }
            allItemsString = Marshal.AllocHGlobal(128);
            MemoryHelper.WriteString(allItemsString, "All Items (Excluding Equipped)", Encoding.UTF8);
            alternateList = Marshal.AllocHGlobal(128);

            base.Setup();
        }

        public void DefaultSorting() {
            Config.ShowAllSorting = new int[SortMethods.Count];
            for (var i = 0; i < SortMethods.Count; i++) {
                var key = i + 1;
                Config.ShowAllSorting[i] = SortMethods[i].DefaultDescending ? -key : key;
            }
        }

        protected override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();

            if (Config.ShowAllSorting == null) DefaultSorting();

            Common.CloseAddon("SalvageItemSelector");
            updateItemHook ??= Common.Hook<UpdateItemDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 49 8B 38", UpdateItemDetour);
            updateItemHook?.Enable();

            setupDropDownList ??= Common.Hook<SetupDropDownList>("E8 ?? ?? ?? ?? 8D 4F 55", SetupDropDownListDetour);
            setupDropDownList?.Enable();
            
            populateHook ??= Common.Hook<PopulateItemList>("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 83 66 2C FE", PopulateDetour);
            populateHook?.Enable();
            callbackHook ??= Common.Hook<Callback>("E8 ?? ?? ?? ?? 8B 4C 24 20 0F B6 D8", CallbackDetour);
            callbackHook?.Enable();
            base.Enable();
        }

        private void* CallbackDetour(AtkUnitBase* atkUnitBase, int count, AtkValue* values, void* a4) {
            if (!(Config.ShowAll && Config.ShowAllDefault)) goto Original;
            if (atkUnitBase != Common.GetUnitBase("SalvageItemSelector")) goto Original;
            if (count != 2) goto Original;
            if (values->Type != ValueType.Int || values->Int != 11) goto Original;
            var value2 = values + 1;
            if (value2->Type != ValueType.Int) goto Original;

            if (value2->Int == 0) {
                value2->Int = 8;
            } else {
                value2->Int -= 1; 
            }
            
            Original:
            return callbackHook.Original(atkUnitBase, count, values, a4);
        }

        [AddonPostSetup("SalvageItemSelector")]
        private void CommonOnAddonPreSetup(AtkUnitBase* addon) {
            Common.GenerateCallback(addon, 11, 0);
        }

        private class ItemEntry {
            public InventoryItem* Slot;
            public Item? Item;
        }

        private bool SetupAllItemList(AgentSalvage* agentSalvage) {
            if (!agentSalvage->AgentInterface.IsAgentActive()) return false;
            if (agentSalvage->SelectedCategory != (AgentSalvage.SalvageItemCategory)8) return true;
            agentSalvage->ItemCount = 0;
            var inventoryManager = InventoryManager.Instance();
            var itemSheet = Service.Data.Excel.GetSheet<Item>();
            if (itemSheet == null) return true;

            var items = new List<ItemEntry>();

            var searchInventories = new List<InventoryType>() {
                InventoryType.Inventory1,
                InventoryType.Inventory2,
                InventoryType.Inventory3,
                InventoryType.Inventory4,
            };

            if (!Config.ShowAllExcludeArmoury) {
                searchInventories.AddRange(new[]{
                    InventoryType.ArmoryMainHand,
                    InventoryType.ArmoryOffHand,
                    InventoryType.ArmoryHead,
                    InventoryType.ArmoryBody,
                    InventoryType.ArmoryHands,
                    InventoryType.ArmoryLegs,
                    InventoryType.ArmoryEar,
                    InventoryType.ArmoryFeets,
                    InventoryType.ArmoryNeck,
                    InventoryType.ArmoryWrist,
                    InventoryType.ArmoryRings
                });
            }

            // Get all items
            foreach (var inventoryType in searchInventories) {
                var inventory = inventoryManager->GetInventoryContainer(inventoryType);

                for (var i = 0; i < inventory->Size; i++) {
                    var slot = inventory->GetInventorySlot(i);
                    if (slot->ItemID == 0) continue;
                    if (slot->Quantity == 0) continue;
                    var item = itemSheet.GetRow(slot->ItemID);
                    if (item == null) continue;
                    if (item.Desynth == 0) continue;

                    if (Config.ShowAllExcludeNoSkill) {
                        var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(item.ClassJobRepair.Row);
                        if (desynthLevel >= item.LevelItem.Row + 50 || desynthLevel >= maxDesynthLevel) continue;
                    }

                    if (Config.ShowAllExcludeGearset && GetGearSetWithItem(slot) != null) continue;

                    items.Add(new ItemEntry() { Slot = slot, Item = item });
                }
            }

            // Apply Sorting
            var sortedItems = items.OrderBy(a => 0);
            if (Config.ShowAllSorting == null) DefaultSorting();
            if (Config.ShowAllSorting != null) {
                for (var i = 0; i < Config.ShowAllSorting.Length; i++) {
                    var key = Config.ShowAllSorting[i];
                    var desc = false;
                    if (key < 0) {
                        key = -key;
                        desc = true;
                    }
                    if (key == 0 || key > SortMethods.Count) continue;
                    var sortMethod = SortMethods[key - 1];
                    sortedItems = desc ? sortedItems.ThenByDescending(sortMethod.ApplySorting) : sortedItems.ThenBy(sortMethod.ApplySorting);
                }
            }

            items = sortedItems.ToList();

            var hasMissingItem = false;
            
            foreach (var item in items) {
                if (item.Item == null) continue;
                if (agentSalvage->ItemCount >= 140) break;
                var exdRow = ExdModule.GetItemRowById(item.Slot->ItemID);;
                if (exdRow == null) {
                    hasMissingItem = true;
                    continue;
                }
                agentSalvage->ItemListAdd(true, item.Slot->Container, item.Slot->Slot, item.Slot->ItemID, exdRow, item.Slot->Quantity);
            }

            return !hasMissingItem;
        }

        private byte failLimit = 50;
        private void RebuildListUntilSuccess() {
            var agentSalvage = AgentSalvage.Instance();
            if (agentSalvage->SelectedCategory == (AgentSalvage.SalvageItemCategory)8) {
                failLimit--;
                SimpleLog.Log($"Retrying Item List #{50 - failLimit}");
                if (!(SetupAllItemList(agentSalvage) || failLimit == 0)) return;
            }
            Common.FrameworkUpdate -= RebuildListUntilSuccess;
        }
        
        private byte PopulateDetour(AgentSalvage* agentSalvage) {
            Common.FrameworkUpdate -= RebuildListUntilSuccess;
            try {
                if (agentSalvage->SelectedCategory == (AgentSalvage.SalvageItemCategory)8) {
                    var fullSuccess = SetupAllItemList(agentSalvage);
                    if (!fullSuccess) {
                        failLimit = 50;
                        Common.FrameworkUpdate += RebuildListUntilSuccess;
                    }
                    return 1;
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                return 1;
            }
            
            return populateHook.Original(agentSalvage);
        }
        
        private static RaptureGearsetModule.GearsetEntry* GetGearSetWithItem(InventoryItem* slot) {
            var gearSetModule = RaptureGearsetModule.Instance();
            var itemIdWithHQ = slot->ItemID;
            if ((slot->Flags & InventoryItem.ItemFlags.HQ) > 0) itemIdWithHQ += 1000000;
            for (var gs = 0; gs < 101; gs++) {
                var gearSet = gearSetModule->GetGearset(gs);
                if (gearSet == null) continue;
                if (gearSet->ID != gs) break;
                if (!gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                foreach (var i in gearSet->ItemsSpan) {
                    if (i.ItemID == itemIdWithHQ) {
                        return gearSet;
                    }
                }
            }

            return null;
        }

        private void* SetupDropDownListDetour(AtkComponentList* atkComponentList, ushort itemCount, byte** itemLabels, byte unknownBool) {
            if (!Config.ShowAll) goto Original;
            if (alternateList == IntPtr.Zero) goto Original;
            if (allItemsString == IntPtr.Zero) goto Original;
            var addonSalvageItemSelector = Common.GetUnitBase("SalvageItemSelector");
            if (addonSalvageItemSelector == null) goto Original;
            var ddlComponent = (AtkComponentDropDownList*) addonSalvageItemSelector->GetNodeById(4)->GetComponent();
            if (ddlComponent == null) goto Original;
            var lComponent = (AtkComponentList*) ddlComponent->AtkComponentBase.UldManager.SearchNodeById(3)->GetComponent();
            if (lComponent != atkComponentList) goto Original;
            var list = (byte**)alternateList;
            if (list == null) goto Original;
            for (var i = 0; i < itemCount; i++) {
                list[i + (Config.ShowAllDefault ? 1 : 0)] = itemLabels[i];
            }
            list[Config.ShowAllDefault ? 0 : itemCount] = (byte*) allItemsString;
            itemCount++;
            itemLabels = (byte**) alternateList;
            Original:
            return setupDropDownList.Original(atkComponentList, itemCount, itemLabels, unknownBool);
        }

        protected override void Disable() {
            Common.FrameworkUpdate -= RebuildListUntilSuccess;
            SaveConfig(Config);
            updateItemHook?.Disable();
            setupDropDownList?.Disable();
            populateHook?.Disable();
            callbackHook?.Disable();
            Reset();
            base.Disable();
        }

        public override void Dispose() {
            updateItemHook?.Dispose();
            setupDropDownList?.Dispose();
            populateHook?.Dispose();
            callbackHook?.Dispose();
            if (allItemsString != IntPtr.Zero) Marshal.FreeHGlobal(allItemsString);
            if (alternateList != IntPtr.Zero) Marshal.FreeHGlobal(alternateList);
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
            var addon = (AddonSalvageItemSelector*) Service.GameGui.GetAddonByName("SalvageItemSelector", 1);
            if (addon != null) {
                if (index > addon->ItemCount) {
                    skillTextNode->SetText("Error");
                    return;
                }
                
                
                var salvageItem = addon->ItemsSpan[(int)index];
                var item = InventoryManager.Instance()->GetInventoryContainer(salvageItem.Inventory)->GetInventorySlot(salvageItem.Slot);
                var itemData = Service.Data.Excel.GetSheet<Item>()?.GetRow(item->ItemID);
                if (itemData == null) return;
                var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(itemData.ClassJobRepair.Row);

                ByteColor c;

                if (desynthLevel >= maxDesynthLevel) {
                    c = Green;
                } else {
                    if (desynthLevel > itemData.LevelItem.Row) {
                        if (Config.YellowForSkillGain && desynthLevel < itemData.LevelItem.Row + 50) {
                            c = Yellow;
                        } else {
                            c = Green;
                        }
                    } else {
                        c = Red;
                    }
                }
                
                skillTextNode->TextColor = c;

                if (Config.Delta) {
                    var desynthDelta = itemData.LevelItem.Row - desynthLevel;
                    skillTextNode->SetText($"{itemData.LevelItem.Row} ({desynthDelta:+#;-#})");
                } else {
                    skillTextNode->SetText($"{desynthLevel:F0}/{itemData.LevelItem.Row}");
                }

                var itemIdWithHQ = item->ItemID;
                if ((item->Flags & InventoryItem.ItemFlags.HQ) > 0) itemIdWithHQ += 1000000;
                var gearsetModule = RaptureGearsetModule.Instance();
                var itemInGearset = false;
                for (var i = 0; i < 101; i++) {
                    var gearset = gearsetModule->GetGearset(i);
                    if (gearset == null) continue;
                    if (gearset->ID != i) break;
                    if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                    foreach (var gsItem in gearset->ItemsSpan) {
                        if (gsItem.ItemID == itemIdWithHQ) {
                            itemInGearset = true;
                            break;
                        }
                    }
                    
                    if (itemInGearset) break;
                }
                desynthRow.CollisionNode->AtkResNode.ToggleVisibility(true);
                if (itemInGearset) {
                    
                    
                    if (Config.BlockClickOnGearset) {
                        desynthRow.CollisionNode->AtkResNode.ToggleVisibility(false);
                    }
                    desynthRow.GearsetWarningNode->SetText($"{(char) SeIconChar.BoxedStar}");
                } else {
                    desynthRow.GearsetWarningNode->SetText("");
                    
                }
                
            }
        }

        public class DesynthRow {
            public AtkTextNode* SkillTextNode;
            public AtkTextNode* GearsetWarningNode;
            public AtkCollisionNode* CollisionNode;
        }

        private Dictionary<ulong, DesynthRow> desynthRows = new Dictionary<ulong, DesynthRow>();

        
        [AddonPostRefresh("SalvageItemSelector")]
        private void Update() {

            var atkUnitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("SalvageItemSelector", 1);

            var dropDownList = (AtkComponentDropDownList*) atkUnitBase->GetNodeById(4)->GetComponent();
            var list = (AtkComponentList*) dropDownList->AtkComponentBase.UldManager.SearchNodeById(3)->GetComponent();
            
            if (atkUnitBase == null) return;
            if ((atkUnitBase->Flags & 0x20) != 0x20) return;

            var nodeList = atkUnitBase->UldManager.NodeList;
            var windowNode = (AtkComponentNode*)atkUnitBase->UldManager.NodeList[1];

            if (windowNode->AtkResNode.Width == 600) {
                desynthRows.Clear();
                UiHelper.SetWindowSize(windowNode, NewWidth, null);
                UiHelper.SetSize(nodeList[0], NewWidth, null);
                UiHelper.SetSize(nodeList[3], NewWidth - 32, null);
                UiHelper.SetSize(nodeList[4], NewWidth - 25, null);
                UiHelper.SetSize(nodeList[5], NewWidth - 32, null);
                UiHelper.SetSize(nodeList[2], nodeList[2]->Width + AddedWidth, null);
                var listComponent = (AtkComponentNode*)atkUnitBase->UldManager.NodeList[3];
                var listNodeList = listComponent->Component->UldManager.NodeList;
                UiHelper.SetSize(listNodeList[0], NewWidth - 32, null);
                UiHelper.SetPosition(listNodeList[1], NewWidth - 40, null);


                UiHelper.ExpandNodeList(atkUnitBase, 2);
                var newHeaderItem = (AtkTextNode*)UiHelper.CloneNode(nodeList[6]);
                newHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newHeaderItem->NodeText.BufSize);
                newHeaderItem->SetText("Skill");

                newHeaderItem->AtkResNode.X = NewWidth - (AddedWidth + 60);
                newHeaderItem->AtkResNode.Width = AddedWidth;
                newHeaderItem->AtkResNode.ParentNode = nodeList[5];
                newHeaderItem->AtkResNode.NextSiblingNode = nodeList[8];
                nodeList[8]->PrevSiblingNode = (AtkResNode*)newHeaderItem;
                atkUnitBase->UldManager.NodeList[atkUnitBase->UldManager.NodeListCount++] = (AtkResNode*)newHeaderItem;

                var gsHeaderItem = (AtkTextNode*)UiHelper.CloneNode(nodeList[6]);
                gsHeaderItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)gsHeaderItem->NodeText.BufSize);
                gsHeaderItem->SetText("Gear\nSet");
                gsHeaderItem->TextFlags |= (byte) TextFlags.MultiLine;
                gsHeaderItem->AtkResNode.X = NewWidth - 80;
                gsHeaderItem->AlignmentFontType = (byte) AlignmentType.Bottom;
                gsHeaderItem->AtkResNode.Width = 30;
                gsHeaderItem->AtkResNode.ParentNode = nodeList[5];
                gsHeaderItem->AtkResNode.NextSiblingNode = (AtkResNode*) newHeaderItem;
                newHeaderItem->AtkResNode.PrevSiblingNode = (AtkResNode*)gsHeaderItem;
                atkUnitBase->UldManager.NodeList[atkUnitBase->UldManager.NodeListCount++] = (AtkResNode*)gsHeaderItem;
                
                for (var i = 2; i < 18; i++) {
                    var listItem = (AtkComponentNode*)listNodeList[i];
                    var listItemNodes = listItem->Component->UldManager.NodeList;
                    
                    listItem->AtkResNode.SetWidth(NewWidth - 40);
                    listItemNodes[0]->SetWidth(NewWidth - 59);
                    listItemNodes[1]->SetWidth(NewWidth - 59);
                    listItemNodes[2]->SetWidth(NewWidth - 40);

                    UiHelper.ExpandNodeList(listItem, 2);
                     
                    var newRowItem = (AtkTextNode*)UiHelper.CloneNode(listItemNodes[3]);
                    newRowItem->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)newRowItem->NodeText.BufSize);
                    newRowItem->SetText("Error");
                    newRowItem->AtkResNode.X = NewWidth - (AddedWidth + 60);
                    newRowItem->AtkResNode.Width = AddedWidth;
                    newRowItem->AtkResNode.ParentNode = (AtkResNode*)listItem;
                    newRowItem->AtkResNode.NextSiblingNode = listItemNodes[7];
                    newRowItem->AlignmentFontType = (byte)AlignmentType.Center;
                    listItemNodes[7]->PrevSiblingNode = (AtkResNode*)newRowItem;
                    listItem->Component->UldManager.NodeList[listItem->Component->UldManager.NodeListCount++] = (AtkResNode*)newRowItem;
                    
                    var gearsetWarning = (AtkTextNode*)UiHelper.CloneNode(listItemNodes[3]);
                    gearsetWarning->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)gearsetWarning->NodeText.BufSize);
                    gearsetWarning->SetText("?");
                    gearsetWarning->AtkResNode.X = NewWidth - 80;
                    gearsetWarning->AtkResNode.Width = 30;
                    gearsetWarning->AtkResNode.ParentNode = (AtkResNode*)listItem;
                    gearsetWarning->AtkResNode.NextSiblingNode = (AtkResNode*) newRowItem;
                    gearsetWarning->AlignmentFontType = (byte)AlignmentType.Center;
                    newRowItem->AtkResNode.PrevSiblingNode = (AtkResNode*) gearsetWarning;
                    listItem->Component->UldManager.NodeList[listItem->Component->UldManager.NodeListCount++] = (AtkResNode*)gearsetWarning;
                    
                    desynthRows.Add((ulong) listItem->Component, new DesynthRow() {
                        SkillTextNode = newRowItem,
                        GearsetWarningNode = gearsetWarning,
                        CollisionNode =  (AtkCollisionNode*) listItemNodes[0],
                    });
                }
            }
        }

        public void Reset() {
            Common.CloseAddon("SalvageItemSelector");
        }
    }
}
