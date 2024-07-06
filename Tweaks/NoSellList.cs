using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

// TODO: Add a context menu to items

[TweakName("No Sell List")]
[TweakDescription("Allows you to define a list of items that can not be sold to a vendor.")]
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

    public Configs Config { get; private set; }

    private string inputNewItemName = string.Empty;
    private string addItemError = string.Empty;

    private Item[] matchedItems = [];

    private void DrawConfig(ref bool hasChanged) {
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
                                    var i = Service.Data.Excel.GetSheet<Item>()?.Where(i => i.Name.RawString == teamcraftMatch.Groups[2].Value).FirstOrDefault();
                                    if (i == null) continue;
                                    itemList.Add(i.RowId);
                                } else {
                                    var i = Service.Data.Excel.GetSheet<Item>()?.Where(i => i.Name.RawString == l).FirstOrDefault();
                                    if (i == null) continue;
                                    itemList.Add(i.RowId);
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

                            var items = Service.Data.Excel.GetSheet<Item>()?.Where(a => {
                                if (idValid && a.RowId == id) return true;
                                return string.Equals(a.Name?.RawString, inputNewItemName, StringComparison.CurrentCultureIgnoreCase);
                            }).ToArray();

                            if (items == null || items.Length == 0) {
                                addItemError = "No item found";
                            } else if (items.Length > 1) {
                                addItemError = "Multiple matches found. Please select one.";
                                matchedItems = items;
                            } else if (!itemList.Add(items[0].RowId)) {
                                addItemError = "Item already in list";
                            } else {
                                inputNewItemName = string.Empty;
                            }

                            changed = true;
                        }
                    }

                    var clickedMatch = false;
                    foreach (var item in matchedItems) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"[{item.RowId}] {item.Name.RawString}");
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
                        var itemInfo = Service.Data.Excel.GetSheet<Item>()?.GetRow(item);
                        if (string.IsNullOrEmpty(itemInfo?.Name?.RawString)) continue;
                        ImGui.TableNextColumn();
                        ImGui.Text($"[{itemInfo.RowId}] {itemInfo.Name.RawString}");
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
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(slot->ItemId);
                        if (!string.IsNullOrEmpty(item?.Name?.RawString)) {
                            Service.Toasts.ShowError($"{item.Name.RawString} is locked by {Name} in {Plugin.Name}.");
                            return false;
                        }
                    }

                    var customListMatch = Config.CustomLists.FirstOrDefault(t => t.Enabled && t.NoSellList.Any(i => i == slot->ItemId));
                    if (customListMatch != null) {
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(slot->ItemId);
                        if (!string.IsNullOrEmpty(item?.Name?.RawString)) {
                            Service.Toasts.ShowError(new SeString(new TextPayload(item.Name.ToDalamudString().TextValue), new TextPayload(" is locked by "), new TextPayload(customListMatch.Name), new NewLinePayload(), new TextPayload(Name), new TextPayload(" in "), new TextPayload(Plugin.Name), new TextPayload(".")));
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

    private delegate void SellItemFromRetainer(void* a1, int a2, InventoryType a3);

    private delegate void SellItemFromInventory(int a2, InventoryType a3);

    private HookWrapper<SellItemFromRetainer> sellItemFromRetainerHook;
    private HookWrapper<SellItemFromInventory> sellItemFromInventoryHook;

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        sellItemFromRetainerHook = Common.Hook<SellItemFromRetainer>("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 80 B9 ?? ?? ?? ?? ?? 41 8B F0", SellItemFromRetainerDetour);
        sellItemFromRetainerHook?.Enable();

        sellItemFromInventoryHook = Common.Hook<SellItemFromInventory>("48 89 5C 24 10 48 89 6C 24 18 56 48 83 EC 20 8B E9", SellItemFromInventoryDetour);
        sellItemFromInventoryHook?.Enable();

        base.Enable();
    }

    private void SellItemFromInventoryDetour(int slotIndex, InventoryType inventory) {
        if (CanSell(slotIndex, inventory)) {
            sellItemFromInventoryHook.Original(slotIndex, inventory);
        }
    }

    private void SellItemFromRetainerDetour(void* a1, int slotIndex, InventoryType inventory) {
        if (CanSell(slotIndex, inventory)) {
            sellItemFromRetainerHook.Original(a1, slotIndex, inventory);
        }
    }

    protected override void Disable() {
        sellItemFromRetainerHook?.Disable();
        sellItemFromInventoryHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        sellItemFromRetainerHook?.Dispose();
        sellItemFromInventoryHook?.Dispose();
        base.Dispose();
    }
}
