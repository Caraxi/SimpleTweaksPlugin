using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public VK[] CopyHotkey = { VK.Ctrl, VK.C };
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class CopyHotkey : TooltipTweaks.SubTweak {



        public override string Name => "Copy Item Name Hotkey";
        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, TooltipTweaks.ItemInfo itemInfo) {
            var seStr = tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay];
            if (seStr == null) return;
            seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", PluginConfig.TooltipTweaks.CopyHotkey.Select(k => k.GetKeyName()))}  Copy item name"));
            tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay] = seStr;
        }

        private bool settingKey = false;
        private bool focused = false;
        private readonly List<VK> newKeys = new List<VK>();
        public override void DrawConfig(ref bool hasChanged) {
            base.DrawConfig(ref hasChanged);

            if (Enabled) {
                ImGui.SameLine();
                var strKeybind = string.Join("+", PluginConfig.TooltipTweaks.CopyHotkey.Select(k => k.GetKeyName()));
                
                ImGui.SetNextItemWidth(100);

                if (settingKey) {
                    for (var k = 0; k < ImGui.GetIO().KeysDown.Count && k < 160; k++) {
                        if (ImGui.GetIO().KeysDown[k]) {
                            if (!newKeys.Contains((VK) k)) {

                                if ((VK) k == VK.ESCAPE) {
                                    settingKey = false;
                                    newKeys.Clear();
                                    focused = false;
                                    break;
                                }
                                
                                newKeys.Add((VK) k);
                                newKeys.Sort();
                            }
                        }
                    }

                    strKeybind = string.Join("+", newKeys.Select(k => k.GetKeyName()));
                }

                if (settingKey) {
                    ImGui.PushStyleColor(ImGuiCol.Border, 0xFF00A5FF);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2);
                }

                ImGui.InputText($"###{GetType().Name}hotkeyDisplay", ref strKeybind, 100, ImGuiInputTextFlags.ReadOnly);
                var active = ImGui.IsItemActive();
                if (settingKey) {

                    ImGui.PopStyleColor(1);
                    ImGui.PopStyleVar();

                    if (!focused) {
                        ImGui.SetKeyboardFocusHere();
                        focused = true;
                    } else {
                        ImGui.SameLine();
                        if (ImGui.Button(newKeys.Count > 0 ? "Confirm" : "Cancel")) {
                            settingKey = false;
                            if (newKeys.Count > 0) PluginConfig.TooltipTweaks.CopyHotkey = newKeys.ToArray();
                            newKeys.Clear();
                            hasChanged = true;
                        } else {
                            if (!active) {
                                focused = false;
                                settingKey = false;
                                if (newKeys.Count > 0) PluginConfig.TooltipTweaks.CopyHotkey = newKeys.ToArray();
                                hasChanged = true;
                                newKeys.Clear();
                            }
                        }
                    }
                } else {
                    ImGui.SameLine();
                    if (ImGui.Button("Set Keybind")) {
                        settingKey = true;
                    }
                }

            }

        }

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnOnUpdateEvent;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnOnUpdateEvent;
            base.Disable();
        }

        private void FrameworkOnOnUpdateEvent(Framework framework) {
            try {
                if (PluginInterface.Framework.Gui.HoveredItem != 0 && PluginConfig.TooltipTweaks.CopyHotkey.All(k => PluginInterface.ClientState.KeyState[(int) k])) {

                    var id = PluginInterface.Framework.Gui.HoveredItem;
                    if (id < 2000000) {
                        id %= 500000;
                        var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);
                        if (item != null) {
                            System.Windows.Forms.Clipboard.SetText(item.Name);
                            foreach (var k in PluginConfig.TooltipTweaks.CopyHotkey) {
                                PluginInterface.ClientState.KeyState[(int) k] = false;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }
    }
}
