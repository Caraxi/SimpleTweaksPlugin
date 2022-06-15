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

public unsafe class QuickSellItems : Tweak {

    public override string Name => "Quick Sell Items at Vendors";
    public override string Description => "Hold a modifier key to sell items from your inventory in one click.";

    public class Configs : TweakConfig {
        public bool Shift = true;
        public bool Ctrl = false;
        public bool Alt = false;
    }

    private delegate void* OpenInventoryContext(AgentInventoryContext* agent, InventoryType inventory, ushort slot, int a4, ushort a5, byte a6);
    private HookWrapper<OpenInventoryContext> openInventoryContextHook;
    
    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Modifier Keys to Auto Sell:");
        ImGui.Dummy(Vector2.Zero);
        ImGui.Indent();
        ImGui.BeginGroup();
        hasChanged |= ImGui.Checkbox("Shift", ref Config.Shift);
        hasChanged |= ImGui.Checkbox("Ctrl", ref Config.Ctrl);
        hasChanged |= ImGui.Checkbox("Alt", ref Config.Alt);
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

        
        ImGui.Text("You may still receive a prompt to confirm for some items.\nThis can be disabled in the Character Configuration.");
    };

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

    private string sellText = "Sell";
    
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        var sellRow = Service.Data.Excel.GetSheet<Addon>()?.GetRow(93);
        if (sellRow != null) sellText = sellRow.Text?.RawString ?? "Sell";

        openInventoryContextHook ??= Common.Hook<OpenInventoryContext>("83 B9 ?? ?? ?? ?? ?? 7E 11", OpenInventoryContextDetour);
        openInventoryContextHook?.Enable();
        base.Enable();
    }

    private bool HotkeyIsHeld => (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt) && (Config.Ctrl || Config.Shift || Config.Ctrl);
    
    private void* OpenInventoryContextDetour(AgentInventoryContext* agent, InventoryType inventoryType, ushort slot, int a4, ushort a5, byte a6) {
        var retVal = openInventoryContextHook.Original(agent, inventoryType, slot, a4, a5, a6);

        try {
            if (CanSellFrom.Contains(inventoryType) && HotkeyIsHeld && Common.GetUnitBase("Shop") != null) {
                var inventory = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
                if (inventory != null) {
                    var itemSlot = inventory->GetInventorySlot(slot);
                    if (itemSlot != null) {
                        var itemId = itemSlot->ItemID;
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
                        if (item != null) {
                            var addonId = agent->AgentInterface.GetAddonID();
                            if (addonId == 0) return retVal;
                            var addon = Common.GetAddonByID(addonId);
                            if (addon == null) return retVal;

                            for (var i = 0; i < agent->ContextItemCount; i++) {
                                var contextItemParam = agent->EventParamsSpan[agent->ContexItemStartIndex + i];
                                if (contextItemParam.Type != ValueType.String) continue;
                                var contextItemName = contextItemParam.ValueString();
                                
                                if (contextItemName == sellText) {
                                    Common.GenerateCallback(addon, 0, i, 0U, 0, 0);
                                    agent->AgentInterface.Hide();
                                    UiHelper.Close(addon);
                                    return retVal;
                                }
                            }
                        }
                    }
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        
        return retVal;
    }

    public override void Disable() {
        openInventoryContextHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        openInventoryContextHook?.Dispose();
        base.Dispose();
    }
}

