using System;
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

        private IntPtr examineIsValidPtr = IntPtr.Zero;

        public override void Setup() {

            try {
                inventoryManager = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
                var a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
                if (a == IntPtr.Zero) throw new Exception("Failed to find GetInventoryContainer");
                getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(a);
                a = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");
                if (a == IntPtr.Zero) throw new Exception("Failed to find GetContainerSlot");
                getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(a);
                examineIsValidPtr = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 C7 43 ?? ?? ?? ?? ??");
                Ready = true;
            } catch {
                PluginLog.Log("Failed to find address for ExamineItemLevel");
            }
        }
        private unsafe void DrawUI() {
            if (!Ready) return;
            var inaccurate = false;
            if (examineIsValidPtr == IntPtr.Zero) return;
            if (*(byte*) (examineIsValidPtr + 0x2A8) == 0) return;
            var container = getInventoryContainer(inventoryManager, 2009);
            if (container == IntPtr.Zero) return;
            var ui = PluginInterface.Framework.Gui.GetAddonByName("CharacterInspect", 1);
            if (ui == null) return;
            ImGui.SetNextWindowSize(new Vector2(ui.Scale * 350, ui.Scale * 540), ImGuiCond.Always);
            ImGui.SetNextWindowPos(new Vector2(ui.X, ui.Y), ImGuiCond.Always);
            if (ImGui.Begin("Inspect Display", ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar)) { 
                var sum = 0U;
                for (var i = 0; i < 13; i++) {
                    var slot = getContainerSlot(container, i);
                    if (slot == IntPtr.Zero) continue;
                    var id = *(uint*) (slot + 8);
                    var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(id);
                    if ((item.Unknown89 & 2) == 2) inaccurate = true;
                    if (i == 0 && !canHaveOffhand.Contains(item.ItemUICategory.Row)) {
                        sum += item.LevelItem.Row;
                        i++;
                    }
                    sum += item.LevelItem.Row;
                }


#if DEBUG
                var s = container.ToInt64().ToString("X");
                ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 45);
                ImGui.SetWindowFontScale(1);
                ImGui.InputText("container", ref s, 16, ImGuiInputTextFlags.ReadOnly);
#endif
                var avgItemLevel = sum / 13;
                // Divide by FontGlobalScale to avoid sizes changing due to Dalamud settings
                ImGui.SetWindowFontScale((1.5f / ImGui.GetIO().FontGlobalScale) * ui.Scale);
                var text = $"{avgItemLevel:0000}";
                var textsize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2((ui.Scale * 255) - textsize.X, ui.Scale * 195));
                var pos = ImGui.GetCursorScreenPos();
                
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(inaccurate ? 0xff5a5abc : 0xffbcbf5a), text);
                if (inaccurate) {
                    var m = ImGui.GetMousePos();
                    if (m.X >= pos.X && m.X < pos.X + textsize.X && m.Y >= pos.Y && m.Y <= pos.Y + textsize.Y) {
                        ImGui.SetTooltip("Item level is inaccurate due to variable ilvl items.");
                    }
                }

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
            if (Enabled) Disable();
            Ready = false;
            Enabled = false;
        }
    }
}
