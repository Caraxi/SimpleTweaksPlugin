using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin {
    internal class ExamineItemLevel : Tweak {

        private delegate IntPtr GetInventoryContainer(IntPtr inventoryManager, int inventoryId);
        private delegate IntPtr GetContainerSlot(IntPtr inventoryContainer, int slotId);

        private GetInventoryContainer getInventoryContainer;
        private GetContainerSlot getContainerSlot;

        private IntPtr inventoryManager;

        private readonly uint[] canHaveOffhand = {2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32};

        public override string Name => "Item Level in Examine";

        public override void Setup() {

            try {
                inventoryManager = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
                var a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
                if (a == IntPtr.Zero) throw new Exception("Failed to find GetInventoryContainer");
                getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(a);
                a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");
                if (a == IntPtr.Zero) throw new Exception("Failed to find GetContainerSlot");
                getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(a);
                Ready = true;
            } catch {
                PluginLog.Log("Failed to find address for ExamineItemLevel");
            }
        }

        private unsafe void DrawUI() {
            if (!Ready) return;
            var container = getInventoryContainer(inventoryManager, 2009);
            if (container == IntPtr.Zero) return;
            var ui = PluginInterface.Framework.Gui.GetAddonByName("CharacterInspect", 1);
            if (ui == null) return;
            ImGui.SetNextWindowSize(new Vector2(ui.Scale * 350, ui.Scale * 540), ImGuiCond.Always);
            ImGui.SetNextWindowPos(new Vector2(ui.X, ui.Y), ImGuiCond.Always);
            if (ImGui.Begin("Inspect Display", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar)) {
                var itemLevels = new List<uint>();
                for (var i = 0; i < 13; i++) {
                    var slot = getContainerSlot(container, i);
                    if (slot == IntPtr.Zero) continue;
                    var id = *(uint*) (slot + 8);
                    var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(id);
                    if (i == 0 && !canHaveOffhand.Contains(item.ItemUICategory.Row)) {
                        i++;
                    }
                    itemLevels.Add(item.LevelItem.Row);
                }
                // Divide by FontGlobalScale to avoid sizes changing due to Dalamud settings
                ImGui.SetWindowFontScale((1.5f / ImGui.GetIO().FontGlobalScale) * ui.Scale);
                var text = $"{itemLevels.Average(a => a):0000}";
                ImGui.SetCursorPos(new Vector2((ui.Scale * 260) - ImGui.CalcTextSize(text).X, ui.Scale * 190));
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(0xffbcbf5a), text);
                ImGui.SetWindowFontScale((1.7f / ImGui.GetIO().FontGlobalScale) * ui.Scale);
                var iconSize = ImGui.CalcTextSize($"{(char) SeIconChar.ItemLevel}");
                ImGui.SetCursorPos(new Vector2((ui.Scale * 265) - ImGui.CalcTextSize(text).X - iconSize.X, ui.Scale * 187));
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(0xffa4dffc), $"{(char) SeIconChar.ItemLevel}");
                ImGui.End();
            }
        }

        public override void Enable() {
            if (!Ready) return;
            PluginInterface.UiBuilder.OnBuildUi += this.DrawUI;
            Enabled = true;
        }

        public override void Disable() {
            PluginInterface.UiBuilder.OnBuildUi -= this.DrawUI;
            Enabled = false;
        }

        public override void Dispose() {
            Disable();
            Ready = false;
            Enabled = false;
        }
    }
}
