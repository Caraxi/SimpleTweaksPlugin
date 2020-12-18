using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class ExamineItemLevel : UiAdjustments.SubTweak {

        private delegate IntPtr GetInventoryContainer(IntPtr inventoryManager, int inventoryId);
        private delegate IntPtr GetContainerSlot(IntPtr inventoryContainer, int slotId);

        private GetInventoryContainer getInventoryContainer;
        private GetContainerSlot getContainerSlot;

        private IntPtr inventoryManager;

        private readonly uint[] canHaveOffhand = {2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32};

        public override string Name => "Item Level in Examine";

        private IntPtr examineIsValidPtr = IntPtr.Zero;
        private bool fontBuilt;
        private ImFontPtr font;
        private bool fontLoadFailed;
        private bool fontPushed;
        private Vector2 lastTextSize = Vector2.One;

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
                SimpleLog.Log("Failed to find address for ExamineItemLevel");
            }
        }
        private unsafe void DrawUI() {
            if (!Ready) return;
            try {
                var inaccurate = false;
                if (!fontBuilt && !fontLoadFailed) {
                    PluginInterface.UiBuilder.RebuildFonts();
                    return;
                }

                if (examineIsValidPtr == IntPtr.Zero) return;
                if (*(byte*) (examineIsValidPtr + 0x2A8) == 0) return;
                var container = getInventoryContainer(inventoryManager, 2009);
                if (container == IntPtr.Zero) return;
                var ui = PluginInterface.Framework.Gui.GetAddonByName("CharacterInspect", 1);
                if (ui == null) return;
                var itemDetail = PluginInterface.Framework.Gui.GetAddonByName("ItemDetail", 1);
                var covered = false;

                var textPos = new Vector2((ui.X + 255 * ui.Scale) - lastTextSize.X, ui.Y + 195 * ui.Scale);
                if (itemDetail != null && itemDetail.Visible) {
                    var itemDetailPos = new Vector2(itemDetail.X, itemDetail.Y);
                    var itemDetailSize = new Vector2(itemDetail.Width * itemDetail.Scale, itemDetail.Height * itemDetail.Scale);
#if DEBUG
                    if (ImGui.GetIO().KeyShift) {
                        var dl = ImGui.GetForegroundDrawList();
                        dl.AddRect(textPos, textPos + lastTextSize, 0xFF0000FF);
                        dl.AddRect(itemDetailPos, itemDetailPos + itemDetailSize, 0xFFFF0000);
                    }
#endif
                    covered = !(textPos.X + lastTextSize.X < itemDetailPos.X || textPos.Y + lastTextSize.Y < itemDetailPos.Y || textPos.X > itemDetailPos.X + itemDetailSize.X || textPos.Y > itemDetailPos.Y + itemDetailSize.Y);
                }

                if (covered) return;
                ImGui.SetNextWindowSize(new Vector2(ui.Scale * 350, ui.Scale * 540), ImGuiCond.Always);
                ImGui.SetNextWindowPos(new Vector2(ui.X, ui.Y), ImGuiCond.Always);

                if (ImGui.Begin("Inspect Display###simpleTweaksExamineItemLevelDisplay", ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar)) {
                    var sum = 0U;
                    for (var i = 0; i < 13; i++) {
                        var slot = getContainerSlot(container, i);
                        if (slot == IntPtr.Zero) continue;
                        var id = *(uint*) (slot + 8);
                        var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(id);
                        if ((item.Unknown90 & 2) == 2) inaccurate = true;
                        if (i == 0 && !canHaveOffhand.Contains(item.ItemUICategory.Row)) {
                            sum += item.LevelItem.Row;
                            i++;
                        }

                        sum += item.LevelItem.Row;
                    }

                    var avgItemLevel = sum / 13;


                    if (!fontLoadFailed && fontBuilt && !fontPushed) {
                        ImGui.PushFont(font);
                        fontPushed = true;
                    }

                    // Divide by FontGlobalScale to avoid sizes changing due to Dalamud settings
                    ImGui.SetWindowFontScale((1.0f / ImGui.GetIO().FontGlobalScale) * ui.Scale);
                    var text = $"{avgItemLevel:0000}";
                    var textsize = ImGui.CalcTextSize(text);
                    ImGui.SetCursorPos(new Vector2((ui.Scale * 255) - textsize.X, ui.Scale * 195));
                    var pos = ImGui.GetCursorScreenPos();

                    ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(inaccurate ? 0xff5a5abc : 0xffbcbf5a), text);
                    if (fontPushed) {
                        ImGui.PopFont();
                        fontPushed = false;
                    }

                    if (inaccurate) {
                        var m = ImGui.GetMousePos();
                        if (m.X >= pos.X && m.X < pos.X + textsize.X && m.Y >= pos.Y && m.Y <= pos.Y + textsize.Y) {
                            ImGui.SetTooltip("Item level is inaccurate due to variable ilvl items.");
                        }
                    }

                    ImGui.SetWindowFontScale((1.7f / ImGui.GetIO().FontGlobalScale) * ui.Scale);
                    var iconSize = ImGui.CalcTextSize($"{(char) SeIconChar.ItemLevel}");
                    lastTextSize = textsize + new Vector2(iconSize.X, 0);
                    ImGui.SetCursorPos(new Vector2((ui.Scale * 255) - textsize.X - iconSize.X, ui.Scale * 187));
                    ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(0xffa4dffc), $"{(char) SeIconChar.ItemLevel}");
                    ImGui.End();
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
            
        }

        public override void Enable() {
            if (!Ready) return;
            PluginInterface.UiBuilder.OnBuildUi += this.DrawUI;
            PluginInterface.UiBuilder.OnBuildFonts += this.BuildFonts;
            Enabled = true;
        }

        private void BuildFonts() {
            try {
                if (Plugin.AssemblyLocation == null) return;
                var fontFile = Path.Combine(Path.GetDirectoryName(Plugin.AssemblyLocation), "itemlevel-font.ttf");

                fontBuilt = false;
                if (File.Exists(fontFile)) {
                    try {
                        font = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, 20);
                        fontBuilt = true;
                    } catch (Exception ex) {
                        SimpleLog.Log($"Font failed to load. {fontFile}");
                        SimpleLog.Log(ex.ToString());
                        fontLoadFailed = true;
                    }
                } else {
                    SimpleLog.Log($"Font doesn't exist. {fontFile}");
                    fontLoadFailed = true;
                }
            } catch (Exception ex){
                SimpleLog.Log($"Font failed to load.");
                SimpleLog.Log(ex.ToString());
                fontLoadFailed = true;
            }
           
        }

        public override void Disable() {
            PluginInterface.UiBuilder.OnBuildUi -= this.DrawUI;
            PluginInterface.UiBuilder.OnBuildFonts -= this.BuildFonts;
            Enabled = false;
        }

        public override void Dispose() {
            if (Enabled) Disable();
            Ready = false;
            Enabled = false;
        }
    }
}
