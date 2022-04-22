using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class InventoryGil : UiAdjustments.SubTweak {
    public override string Name => "Show Gil in Inventory";
    public override string Description => "Show your current gil in the inventory windows, as it does with retainers.";

    private HookWrapper<Common.AddonOnUpdate> inventoryUpdateHook;
    private HookWrapper<Common.AddonOnUpdate> largeInventoryUpdateHook;
    private HookWrapper<Common.AddonOnUpdate> expansionInventoryUpdateHook;

    public class Configs : TweakConfig {
        [TweakConfigOption("Colour", "Color")]
        public Vector4 Colour = Common.UiColorToVector4(0xEEE1C5FF);

        [TweakConfigOption("Glow", "Color")]
        public Vector4 Glow = Common.UiColorToVector4(0x000000FF);
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() => Update();

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
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
        if (Config == null) return;
        try { 
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

                var lastNode = atkUnitBase->RootNode->ChildNode;
                if (lastNode == null) return;
                while (true) {
                    if (lastNode->PrevSiblingNode == null) break;
                    lastNode = lastNode->PrevSiblingNode;
                }

                lastNode->PrevSiblingNode = (AtkResNode*) textNode;
                textNode->AtkResNode.NextSiblingNode = lastNode;
                textNode->AtkResNode.ParentNode = lastNode->ParentNode;
                textNode->TextFlags |= (byte)TextFlags.Edge;

                atkUnitBase->UldManager.UpdateDrawNodeList();
            }

            if (textNode == null) return;
            
            var gil = InventoryManager.Instance()->GetItemCountInContainer(1, InventoryType.Currency);
            
            textNode->TextColor.A = (byte) (Config.Colour.W * 255f);
            textNode->TextColor.R = (byte) (Config.Colour.X * 255f);
            textNode->TextColor.G = (byte) (Config.Colour.Y * 255f);
            textNode->TextColor.B = (byte) (Config.Colour.Z * 255f);
            textNode->EdgeColor.A = (byte) (Config.Glow.W * 255f);
            textNode->EdgeColor.R = (byte) (Config.Glow.X * 255f);
            textNode->EdgeColor.G = (byte) (Config.Glow.Y * 255f);
            textNode->EdgeColor.B = (byte) (Config.Glow.Z * 255f);

            textNode->SetText(gil.ToString("N0", Culture) + $"{(char) SeIconChar.Gil}");
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void Cleanup() {
        try {
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
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    public override void Disable() {
        SaveConfig(Config);
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