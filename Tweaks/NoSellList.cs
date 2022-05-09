using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

// TODO: Add a context menu to items when dalamud brings back context menus.

public unsafe class NoSellList : Tweak {
    public override string Name => "No Sell List";
    public override string Description => "Allows you to define a list of items that can not be sold to a vendor.";

    public class Configs : TweakConfig {
        public HashSet<uint> NoSellList = new();
    }

    public Configs Config { get; private set; }

    private string inputNewItemName = string.Empty;
    private string addItemError = string.Empty;

    private Item[] matchedItems = Array.Empty<Item>();

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Locked Items:");

        if (ImGui.BeginTable("lockedItemsTable", 2)) {
            
            ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("###deleteItem", ImGuiTableColumnFlags.WidthFixed, 100);
            
            ImGui.TableHeadersRow();

            var removeId = 0U;
            foreach (var item in Config.NoSellList) {
                var itemInfo = Service.Data.Excel.GetSheet<Item>()?.GetRow(item);
                if (string.IsNullOrEmpty(itemInfo?.Name?.RawString)) continue;
                ImGui.TableNextColumn();
                ImGui.Text($"[{itemInfo.RowId}] {itemInfo.Name.RawString}");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Button($"Remove##removeItem{item}")) removeId = item;
            }
            
            if (Config.NoSellList.Contains(removeId)) {
                Config.NoSellList.Remove(removeId);
                hasChanged = true;
            }

            ImGui.TableNextColumn();
            
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##newItemName", ref inputNewItemName, 150);
            ImGui.Text(addItemError);
            ImGui.TableNextColumn();
            if (ImGui.Button("Add Item")) {
                addItemError = string.Empty;
                var id = 0U;
                var idValid = uint.TryParse(inputNewItemName, out id);

                var items = Service.Data.Excel.GetSheet<Item>()?.Where(a => {
                    if (idValid && a.RowId == id) return true;
                    if (a.Name?.RawString?.ToLower() == inputNewItemName.ToLower()) return true;
                    return false;
                }).ToArray();

                if (items == null || items.Length == 0) {
                    addItemError = "No item found";
                } else if (items.Length > 1) {
                    addItemError = "Multiple matches found. Please select one.";
                    matchedItems = items;
                } else if (Config.NoSellList.Contains(items[0].RowId)) {
                    addItemError = "Item already in list";
                } else {
                    Config.NoSellList.Add(items[0].RowId);
                }
                hasChanged = true;
            }

            var clickedMatch = false;
            foreach (var item in matchedItems) {
                ImGui.TableNextColumn();
                ImGui.Text($"[{item.RowId}] {item.Name.RawString}");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Button($"Add##addItem{item}")) {
                    clickedMatch = true;
                    Config.NoSellList.Add(item.RowId);
                    hasChanged = true;
                }
            }
            
            if (clickedMatch) matchedItems = Array.Empty<Item>();
            
            ImGui.EndTable();
        }
    };

    private delegate void SellItem(ShopEventHandler* a1, void* a2, uint a3);

    private HookWrapper<SellItem> sellItemHook;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        sellItemHook = Common.Hook<SellItem>("40 57 48 83 EC 50 F6 41 24 01", SellItemDetour);
        sellItemHook?.Enable();
        base.Enable();
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0x3340)]
    public struct ShopEventHandler {
        [FieldOffset(0x3324)] public InventoryType InventoryType;
        [FieldOffset(0x331C)] public int InventorySlot;
    }

    private void SellItemDetour(ShopEventHandler* a1, void* a2, uint a3) {
        try {
            var container = InventoryManager.Instance()->GetInventoryContainer(a1->InventoryType);
            if (container != null) {
                var slot = container->GetInventorySlot(a1->InventorySlot);
                if (slot != null) {
                    if (Config.NoSellList.Any(i => i == slot->ItemID)) {
                        a1->InventorySlot = -1;
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(slot->ItemID);
                        if (!string.IsNullOrEmpty(item?.Name?.RawString)) {
                            Service.Toasts.ShowError($"{item.Name.RawString} is locked by {Name} in {Plugin.Name}.");
                        }
                    }
                }
            }
        } catch {
            //
        }
        
        sellItemHook.Original(a1, a2, a3);
    }

    public override void Disable() {
        sellItemHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        sellItemHook?.Dispose();
        base.Dispose();
    }
}

