using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Quick Sell Items at Vendors")]
[TweakDescription("Hold a modifier key to sell items from your inventory in one click.")]
[Changelog("1.9.6.0", "Added option to allow quick selling items from player inventory while using a retainer.")]
[Changelog("1.9.7.0", "Allowed selling from player inventory while accessing retainer's market inventory.")]
public unsafe class QuickSellItems : Tweak {
    
    public class Configs : TweakConfig {
        public bool Shift = true;
        public bool Ctrl = false;
        public bool Alt = false;

        public bool RetainerSell = false;
        public bool SellAtRetainer = false;
    }

    private delegate void* OpenInventoryContext(AgentInventoryContext* agent, InventoryType inventory, ushort slot, int a4, ushort a5, byte a6);
    private HookWrapper<OpenInventoryContext> openInventoryContextHook;
    
    public Configs Config { get; private set; }

    private void DrawConfig() {
        ImGui.Text("Modifier Keys to Auto Sell:");
        ImGui.Dummy(Vector2.Zero);
        ImGui.Indent();
        ImGui.BeginGroup();
        ImGui.Checkbox("Shift", ref Config.Shift);
        ImGui.Checkbox("Ctrl", ref Config.Ctrl);
        ImGui.Checkbox("Alt", ref Config.Alt);
        ImGui.EndGroup();
        var s = ImGui.GetItemRectSize();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(min - ImGui.GetStyle().ItemSpacing, max + ImGui.GetStyle().ItemSpacing, 0x99999999);
        ImGui.SameLine();
        ImGui.BeginGroup();
        var s2 = ImGui.CalcTextSize(" + RIGHT CLICK");
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(new Vector2(s.Y / 2 - s2.Y / 2));
        ImGui.Text(" + RIGHT CLICK");
        ImGui.PopStyleVar();
        
        if (!(Config.Shift || Config.Ctrl || Config.Alt)) {
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFF3333DD);
            ImGui.Text( "  At least one modifier key must be enabled.");
            ImGui.PopStyleColor();
        }
        
        ImGui.EndGroup();
        ImGui.Unindent();
        ImGui.Dummy(Vector2.Zero);
        ImGui.Checkbox("Enable Quick Sell from retainer inventory", ref Config.RetainerSell);
        ImGui.Dummy(Vector2.Zero);
        ImGui.Checkbox("Enable Quick Sell from player inventory at retainer", ref Config.SellAtRetainer);
        ImGui.Dummy(Vector2.Zero);
        
        ImGui.Text("You may still receive a prompt to confirm for some items.\nThis can be disabled in the Character Configuration.");
    }

    public InventoryType[] CanSellFrom = {
        InventoryType.Inventory1,
        InventoryType.Inventory2, 
        InventoryType.Inventory3, 
        InventoryType.Inventory4,
        InventoryType.ArmoryMainHand, 
        InventoryType.ArmoryHead, 
        InventoryType.ArmoryBody, 
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs, 
        InventoryType.ArmoryFeets, 
        InventoryType.ArmoryEar, 
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist, 
        InventoryType.ArmoryRings, 
        InventoryType.ArmoryOffHand
    };
    
    public InventoryType[] RetainerInventories = {
        InventoryType.RetainerPage1,
        InventoryType.RetainerPage2,
        InventoryType.RetainerPage3,
        InventoryType.RetainerPage4,
        InventoryType.RetainerPage5,
        InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
    };

    private string sellText = "Sell";
    private string retainerSellText = "Have Retainer Sell Items";

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        var sellRow = Service.Data.Excel.GetSheet<Addon>()?.GetRow(93);
        if (sellRow != null) sellText = sellRow.Text?.RawString ?? "Sell";

        var retainerSellRow = Service.Data.Excel.GetSheet<Addon>()?.GetRow(5480);
        if (retainerSellRow != null) retainerSellText = retainerSellRow.Text?.RawString ?? "Have Retainer Sell Items";

        openInventoryContextHook ??= Common.Hook<OpenInventoryContext>("83 B9 ?? ?? ?? ?? ?? 7E 11", OpenInventoryContextDetour);
        openInventoryContextHook?.Enable();
        base.Enable();
    }

    private bool HotkeyIsHeld => (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt) && (Config.Ctrl || Config.Shift || Config.Alt);
    
    private void* OpenInventoryContextDetour(AgentInventoryContext* agent, InventoryType inventoryType, ushort slot, int a4, ushort a5, byte a6) {
        var retVal = openInventoryContextHook.Original(agent, inventoryType, slot, a4, a5, a6);

        try {
            bool TrySell(string sellText) {
                var inventory = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
                if (inventory != null) {
                    var itemSlot = inventory->GetInventorySlot(slot);
                    if (itemSlot != null) {
                        var itemId = itemSlot->ItemID;
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
                        if (item != null) {
                            var addonId = agent->AgentInterface.GetAddonID();
                            if (addonId == 0) return false;
                            var addon = Common.GetAddonByID(addonId);
                            if (addon == null) return false;

                            for (var i = 0; i < agent->ContextItemCount; i++) {
                                var contextItemParam = agent->EventParamsSpan[agent->ContexItemStartIndex + i];
                                if (contextItemParam.Type != ValueType.String) continue;
                                var contextItemName = contextItemParam.ValueString();
                                
                                if (contextItemName == sellText) {
                                    Common.GenerateCallback(addon, 0, i, 0U, 0, 0);
                                    agent->AgentInterface.Hide();
                                    UiHelper.Close(addon);
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            if (CanSellFrom.Contains(inventoryType) && HotkeyIsHeld && Common.GetUnitBase("Shop") != null) {
                if (TrySell(sellText)) return retVal;
            }

            if (Config.RetainerSell && RetainerInventories.Contains(inventoryType) && HotkeyIsHeld && Common.GetUnitBase("RetainerGrid0") != null) {
                if (TrySell(retainerSellText)) return retVal;
            }

            if (Config.SellAtRetainer && CanSellFrom.Contains(inventoryType) && HotkeyIsHeld && (Common.GetUnitBase("RetainerGrid0") != null || Common.GetUnitBase("RetainerSellList") != null)) {
                if (TrySell(retainerSellText)) return retVal;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        
        return retVal;
    }

    protected override void Disable() {
        openInventoryContextHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        openInventoryContextHook?.Dispose();
        base.Dispose();
    }
}

