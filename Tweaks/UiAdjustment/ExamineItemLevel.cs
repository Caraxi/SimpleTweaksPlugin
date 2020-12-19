using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {

    public class ExamineItemLevel : UiAdjustments.SubTweak {

        private delegate IntPtr GetInventoryContainer(IntPtr inventoryManager, int inventoryId);
        private delegate IntPtr GetContainerSlot(IntPtr inventoryContainer, int slotId);

        private delegate IntPtr ExamineUpdated(IntPtr a1, int a2, byte a3);
        private Hook<ExamineUpdated> examinedUpdatedHook;
        private IntPtr examineUpdatedAddress;

        private GetInventoryContainer getInventoryContainer;
        private GetContainerSlot getContainerSlot;

        private IntPtr inventoryManager;

        private readonly uint[] canHaveOffhand = { 2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32 };

        public override string Name => "Item Level in Examine";

        private IntPtr examineIsValidPtr = IntPtr.Zero;

        public override void Setup() {

            inventoryManager = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
            var a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
            if (a == IntPtr.Zero) throw new Exception("Failed to find GetInventoryContainer");
            getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(a);
            a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");
            if (a == IntPtr.Zero) throw new Exception("Failed to find GetContainerSlot");
            getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(a);
            examineIsValidPtr = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 C7 43 ?? ?? ?? ?? ??");

            examineUpdatedAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 89 04 9F");

            SimpleLog.Verbose($"ExamineIsValidPtr: {examineIsValidPtr.ToInt64():X}");
            Ready = true;
        }

        public override void Enable() {
            if (!Ready) return;

            examinedUpdatedHook ??= new Hook<ExamineUpdated>(examineUpdatedAddress, new ExamineUpdated(ExamineUpdatedDetour));
            examinedUpdatedHook?.Enable();

            ShowItemLevel();
            Enabled = true;
        }

        private IntPtr ExamineUpdatedDetour(IntPtr a1, int a2, byte a3) {
            var r = examinedUpdatedHook.Original(a1, a2, a3);
            ShowItemLevel();
            return r;
        }

        private readonly IntPtr allocText = Marshal.AllocHGlobal(512);

        private unsafe void ShowItemLevel(bool reset = false) {

            if (examineIsValidPtr == IntPtr.Zero) return;
            if (*(byte*)(examineIsValidPtr + 0x2A8) == 0) return;
            var container = getInventoryContainer(inventoryManager, 2009);
            if (container == IntPtr.Zero) return;

            var examineWindow = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("CharacterInspect", 1);
            if (examineWindow == null) return;
            var node = examineWindow->RootNode;
            if (node == null) return;
            node = node->ChildNode;
            if (node == null) return;

            while (node != null) {
                if (node->Type == (ushort)NodeType.Res) break;
                node = node->PrevSiblingNode;
            }

            if (node == null) return;
            node = node->ChildNode;
            if (node == null) return;

            AtkComponentNode* compNode = null;
            ULDComponentInfo* compInfo = null;

            while (node != null) {
                if (node->Type >= 1000) {
                    compNode = (AtkComponentNode*)node;
                    compInfo = (ULDComponentInfo*)compNode->Component->ULDData.Objects;
                    if (compInfo->ComponentType == (byte)ComponentType.Preview) {
                        break;
                    }
                }
                node = node->PrevSiblingNode;
            }
            if (node == null || compNode == null || compInfo == null) return;

            node = compNode->Component->ULDData.RootNode;
            if (node == null) return;
            while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

            if (node->Type != (ushort)NodeType.Text) return;

            if (reset) {
                node->Flags &= ~0x10;
                return;
            }

            var inaccurate = false;
            var sum = 0U;
            for (var i = 0; i < 13; i++) {
                var slot = getContainerSlot(container, i);
                if (slot == IntPtr.Zero) continue;
                var id = *(uint*)(slot + 8);
                var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(id);
                if ((item.Unknown90 & 2) == 2) inaccurate = true;
                if (i == 0 && !canHaveOffhand.Contains(item.ItemUICategory.Row)) {
                    sum += item.LevelItem.Row;
                    i++;
                }
                sum += item.LevelItem.Row;
            }

            var avgItemLevel = sum / 13;

            var textNode = (AtkTextNode*)node;
            var seStr = new SeString(new List<Payload>() {
                new TextPayload($"{avgItemLevel:0000}"),
            });

            Plugin.Common.WriteSeString((byte*)allocText, seStr);
            textNode->NodeText.StringPtr = (byte*)allocText;

            textNode->FontSize = 14;
            textNode->AlignmentFontType = 37;
            textNode->FontSize = 16;
            textNode->CharSpacing = 0;
            textNode->LineSpacing = 24;
            textNode->TextColor = new FFXIVByteColor() { R = (byte)(inaccurate ? 0xFF : 0x45), G = (byte) (inaccurate ? (byte) 0x83 : (byte) 0xB2), B = (byte) (inaccurate ? 0x75 : 0xAE), A = 0xFF };

            node->Height = 24;
            node->Width = 80;
            node->Flags |= 0x10;
            node->Y = 0;
            node->Flags_2 |= 0x1;
            node->X = 92;
        }

        public override void Disable() {
            examinedUpdatedHook?.Disable();
            ShowItemLevel(true);
            Enabled = false;
        }

        public override void Dispose() {
            if (Enabled) Disable();
            Ready = false;
            Enabled = false;
            examinedUpdatedHook?.Dispose();
            Marshal.FreeHGlobal(allocText);
        }
    }
}
