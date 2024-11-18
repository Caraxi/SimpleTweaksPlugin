using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Utility;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

// TODO: Add a context menu to items

[TweakName("No Sell List")]
[TweakDescription("Allows you to define a list of items that can not be sold to a vendor.")]
[TweakAutoConfig]
public unsafe class NoSellList : Tweak {
    public class Configs : TweakConfig {
        public HashSet<uint> NoSellList = [];
        public List<NoSellItemList> CustomLists = [];
    }

    public class NoSellItemList {
        public bool Enabled;
        public string Name;
        public Guid Id = new();
        public HashSet<uint> NoSellList = [];
    }

    [TweakConfig] public Configs Config { get; private set; }

    private string inputNewItemName = string.Empty;
    private string addItemError = string.Empty;

    private Item[] matchedItems = [];

    protected void DrawConfig(ref bool hasChanged) {
        bool ShowList(HashSet<uint> itemList) {
            var changed = false;
            if (ImGui.BeginChild("noSellItemListView", new Vector2(-1, 200))) {
                if (ImGui.BeginTable("lockedItemsTable", 2)) {
                    ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("###deleteItem", ImGuiTableColumnFlags.WidthFixed, 100);

                    ImGui.TableHeadersRow();
                    ImGui.TableNextColumn();

                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##newItemName", ref inputNewItemName, 150);
                    ImGui.Text(addItemError);
                    ImGui.TableNextColumn();

                    if (ImGui.GetIO().KeyShift) {
                        if (ImGui.Button("Import Clipboard")) {
                            var lines = ImGui.GetClipboardText().Split('\n').Select(l => l.Trim()).ToArray();
                            var teamcraftRegex = new Regex(@"^(\d+)x (.*)$");
                            foreach (var l in lines) {
                                var teamcraftMatch = teamcraftRegex.Match(l);
                                if (teamcraftMatch.Success) {
                                    var i = Service.Data.Excel.GetSheet<Item>().FirstOrNull(i => i.Name.ExtractText() == teamcraftMatch.Groups[2].Value);
                                    if (i == null) continue;
                                    itemList.Add(i.Value.RowId);
                                } else {
                                    var i = Service.Data.Excel.GetSheet<Item>().FirstOrNull(i => i.Name.ExtractText() == l);
                                    if (i == null) continue;
                                    itemList.Add(i.Value.RowId);
                                }
                            }

                            changed = true;
                        }

                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Loads list of items from clipboard. One item per line, supports Teamcraft item list format.");
                        }
                    } else {
                        if (ImGui.Button("Add Item") && !string.IsNullOrWhiteSpace(inputNewItemName)) {
                            addItemError = string.Empty;
                            var idValid = uint.TryParse(inputNewItemName, out var id);

                            var items = Service.Data.Excel.GetSheet<Item>().Where(a => {
                                if (idValid && a.RowId == id) return true;
                                return string.Equals(a.Name.ExtractText(), inputNewItemName, StringComparison.CurrentCultureIgnoreCase);
                            }).ToArray();

                            switch (items.Length) {
                                case 0: addItemError = "No item found"; break;
                                case > 1:
                                    addItemError = "Multiple matches found. Please select one.";
                                    matchedItems = items;
                                    break;
                                default: {
                                    if (!itemList.Add(items[0].RowId)) {
                                        addItemError = "Item already in list";
                                    } else {
                                        inputNewItemName = string.Empty;
                                    }

                                    break;
                                }
                            }

                            changed = true;
                        }
                    }

                    var clickedMatch = false;
                    foreach (var item in matchedItems) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"[{item.RowId}] {item.Name.ExtractText()}");
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.Button($"Add##addItem{item}")) {
                            clickedMatch = true;
                            itemList.Add(item.RowId);
                            changed = true;
                            inputNewItemName = string.Empty;
                            addItemError = string.Empty;
                        }
                    }

                    if (clickedMatch) matchedItems = [];

                    var removeId = 0U;
                    foreach (var item in itemList) {
                        var itemInfo = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(item);
                        if (string.IsNullOrEmpty(itemInfo?.Name.ExtractText())) continue;
                        ImGui.TableNextColumn();
                        ImGui.Text($"[{itemInfo.Value.RowId}] {itemInfo.Value.Name.ExtractText()}");
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.Button($"Remove##removeItem{item}")) removeId = item;
                    }

                    if (itemList.Contains(removeId)) {
                        itemList.Remove(removeId);
                        changed = true;
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.EndChild();

            return changed;
        }

        ImGui.Text("Locked Items:");
        if (ImGui.BeginTabBar("noSellListTabs")) {
            if (ImGui.BeginTabItem("Default List")) {
                hasChanged |= ShowList(Config.NoSellList);
                ImGui.EndTabItem();
            }

            var x = 0;
            foreach (var cl in Config.CustomLists) {
                ImGui.PushID($"customList_{x++}");
                if (!cl.Enabled) {
                    var c = *ImGui.GetStyleColorVec4(ImGuiCol.Text);
                    c *= 0.75f;
                    c.W *= 0.75f;
                    ImGui.PushStyleColor(ImGuiCol.Text, c);
                }

                var tabOpen = ImGui.BeginTabItem($"{cl.Name}###customList_{cl.Id}");
                if (!cl.Enabled) ImGui.PopStyleColor();

                if (tabOpen) {
                    ImGui.Checkbox($"###enabled_{cl.Id}", ref cl.Enabled);
                    ImGui.SameLine();
                    ImGui.InputText($"###name_{cl.Id}", ref cl.Name, 32);
                    ImGui.SameLine();
                    if (ImGui.Button($"Delete List##{cl.Id}") && ImGui.GetIO().KeyShift) {
                        Config.CustomLists.Remove(cl);
                        ImGui.EndTabItem();
                        ImGui.PopID();
                        hasChanged = true;
                        break;
                    }

                    if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift) {
                        ImGui.SetTooltip("Hold SHIFT to confirm delete.");
                    }

                    hasChanged |= ShowList(cl.NoSellList);
                    ImGui.EndTabItem();
                }

                ImGui.PopID();
            }

            if (ImGui.TabItemButton("+")) {
                Config.CustomLists.Add(new NoSellItemList() { Enabled = true, Name = "New List" });
                hasChanged = true;
            }

            ImGui.EndTabBar();
        }
    }

    private bool CanSell(int slotIndex, InventoryType inventory) {
        try {
            var container = InventoryManager.Instance()->GetInventoryContainer(inventory);
            if (container != null) {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot != null) {
                    if (Config.NoSellList.Any(i => i == slot->ItemId)) {
                        var item = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(slot->ItemId);
                        if (item == null) return false;
                        if (!string.IsNullOrEmpty(item.Value.Name.ExtractText())) {
                            Service.Toasts.ShowError($"{item.Value.Name.ExtractText()} is locked by {Name} in {Plugin.Name}.");
                            return false;
                        }
                    }

                    var customListMatch = Config.CustomLists.FirstOrDefault(t => t.Enabled && t.NoSellList.Any(i => i == slot->ItemId));
                    if (customListMatch != null) {
                        var item = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(slot->ItemId);
                        if (item == null) return false;
                        if (!string.IsNullOrEmpty(item.Value.Name.ExtractText())) {
                            Service.Toasts.ShowError(new SeString(new TextPayload(item.Value.Name.ExtractText()), new TextPayload(" is locked by "), new TextPayload(customListMatch.Name), new NewLinePayload(), new TextPayload(Name), new TextPayload(" in "), new TextPayload(Plugin.Name), new TextPayload(".")));
                            return false;
                        }
                    }
                }
            }
        } catch {
            //
        }

        return true;
    }

    private delegate void SellItemFromVirtual(void* a1, int a2, InventoryType a3);

    private delegate void SellItemFromInventory(int a2, InventoryType a3);

    [TweakHook, Signature("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 80 B9 ?? ?? ?? ?? ?? 41 8B F0", DetourName = nameof(SellItemFromRetainerDetour))]
    private HookWrapper<SellItemFromVirtual> sellItemFromRetainerHook;

    [TweakHook, Signature("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 41 8B F0 8B EA E8", DetourName = nameof(SellItemFromInventoryVFuncDetour))]
    private HookWrapper<SellItemFromVirtual> sellItemFromInventoryVFuncHook;
    
    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 33 C0 89 06", DetourName = nameof(SellItemFromInventoryDetour))]
    private HookWrapper<SellItemFromInventory> sellItemFromInventoryHook; // SE why is this still used for drag & drop selling?

    private void SellItemFromInventoryDetour(int slotIndex, InventoryType inventory) {
        SimpleLog.Verbose($"Attempting to sell from {inventory}:{slotIndex} [Drag & Drop]");
        if (CanSell(slotIndex, inventory)) {
            sellItemFromInventoryHook.Original(slotIndex, inventory);
        }
    }

    private void SellItemFromRetainerDetour(void* a1, int slotIndex, InventoryType inventory) {
        SimpleLog.Verbose($"Attempting to sell from {inventory}:{slotIndex} [Retainer]");
        if (CanSell(slotIndex, inventory)) {
            sellItemFromRetainerHook.Original(a1, slotIndex, inventory);
        }
    }
    
    private void SellItemFromInventoryVFuncDetour(void* a1, int slotIndex, InventoryType inventory) {
        SimpleLog.Verbose($"Attempting to sell from {inventory}:{slotIndex} [Inventory]");
        if (CanSell(slotIndex, inventory)) {
            sellItemFromInventoryVFuncHook.Original(a1, slotIndex, inventory);
        }
    }
}
