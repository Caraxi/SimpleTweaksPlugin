using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Quick Sell Items at Vendors")]
[TweakDescription("Hold a modifier key to sell items from your inventory in one click.")]
[Changelog("1.9.6.0", "Added option to allow quick selling items from player inventory while using a retainer.")]
[Changelog("1.9.7.0", "Allowed selling from player inventory while accessing retainer's market inventory.")]
[TweakAutoConfig]
public unsafe class QuickSellItems : Tweak {
    public class Configs : TweakConfig {
        public bool Shift = true;
        public bool Ctrl;
        public bool Alt;

        public bool RetainerSell;
        public bool SellAtRetainer;
    }

    [TweakHook(typeof(AgentInventoryContext), nameof(AgentInventoryContext.OpenForItemSlot), nameof(OpenInventoryContextDetour))]
    private HookWrapper<AgentInventoryContext.Delegates.OpenForItemSlot> openInventoryContextHook;

    [TweakConfig] public Configs Config { get; private set; }

    protected void DrawConfig() {
        ImGui.Text("Modifier Keys to Auto Sell:");
        ImGui.Spacing();
        using (ImRaii.PushIndent()) {
            using (ImRaii.Group()) {
                ImGui.Checkbox("Shift", ref Config.Shift);
                ImGui.Checkbox("Ctrl", ref Config.Ctrl);
                ImGui.Checkbox("Alt", ref Config.Alt);
            }
            
            var s = ImGui.GetItemRectSize();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRect(min - ImGui.GetStyle().ItemSpacing, max + ImGui.GetStyle().ItemSpacing, 0x99999999);
            ImGui.SameLine();

            using (ImRaii.Group()) {
                var s2 = ImGui.CalcTextSize(" + RIGHT CLICK");
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                ImGui.Dummy(new Vector2(s.Y / 2 - s2.Y / 2));
                ImGui.Text(" + RIGHT CLICK");
                ImGui.PopStyleVar();

                if (!(Config.Shift || Config.Ctrl || Config.Alt)) {
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF3333DD);
                    ImGui.Text("  At least one modifier key must be enabled.");
                    ImGui.PopStyleColor();
                }
            }
        }
        
        ImGui.Checkbox("Enable Quick Sell from retainer inventory", ref Config.RetainerSell);
        ImGui.Checkbox("Enable Quick Sell from player inventory at retainer", ref Config.SellAtRetainer);
        ImGui.Spacing();
        ImGui.Text("You may still receive a prompt to confirm for some items.\nThis can be disabled in the Character Configuration.");
    }

    public InventoryType[] CanSellFrom = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.ArmoryMainHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmoryOffHand];

    public InventoryType[] RetainerInventories = [InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4, InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7];

    private string sellText = "Sell";
    private string retainerSellText = "Have Retainer Sell Items";

    protected override void Enable() {
        var sellRow = Service.Data.Excel.GetSheet<Addon>().GetRowOrNull(93);
        if (sellRow != null) sellText = sellRow.Value.Text.ExtractText();

        var retainerSellRow = Service.Data.Excel.GetSheet<Addon>().GetRowOrNull(5480);
        if (retainerSellRow != null) retainerSellText = retainerSellRow.Value.Text.ExtractText();
    }

    private bool HotkeyIsHeld => (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt) && (Config.Ctrl || Config.Shift || Config.Alt);

    private void OpenInventoryContextDetour(AgentInventoryContext* agent, uint inventoryId, int slot, int a4, uint addonId) {
        openInventoryContextHook.Original(agent, inventoryId, slot, a4, addonId);
        var inventoryType = (InventoryType)inventoryId;
        try {
            bool TrySell(string sellText) {
                var inventory = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
                if (inventory == null) return false;
                var itemSlot = inventory->GetInventorySlot(slot);
                if (itemSlot == null) return false;
                var itemId = itemSlot->ItemId;
                var item = Service.Data.Excel.GetSheet<Item>()?.GetRowOrNull(itemId);
                if (item == null) return false;
                var agentAddonId = agent->AgentInterface.GetAddonId();
                if (agentAddonId == 0) return false;
                var addon = Common.GetAddonByID(agentAddonId);
                if (addon == null) return false;

                for (var i = 0; i < agent->ContextItemCount; i++) {
                    var contextItemParam = agent->EventParams[agent->ContexItemStartIndex + i];
                    if (contextItemParam.Type != ValueType.String) continue;
                    var contextItemName = contextItemParam.ValueString();

                    if (contextItemName != sellText) continue;
                    Common.GenerateCallback(addon, 0, i, 0U, 0, 0);
                    agent->AgentInterface.Hide();
                    UiHelper.Close(addon);
                    return true;
                }

                return false;
            }

            if ((CanSellFrom.Contains(inventoryType) && HotkeyIsHeld && Common.GetUnitBase("Shop") != null && TrySell(sellText)) ||
                (Config.RetainerSell && RetainerInventories.Contains(inventoryType) && HotkeyIsHeld && Common.GetUnitBase("RetainerGrid0") != null && TrySell(retainerSellText)) || 
                (Config.SellAtRetainer && CanSellFrom.Contains(inventoryType) && HotkeyIsHeld && (Common.GetUnitBase("RetainerGrid0") != null || Common.GetUnitBase("RetainerSellList") != null) && TrySell(retainerSellText))) {
                SimpleLog.Debug($"Quick Sell from {inventoryType}.Slot{slot:00}");
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
}
