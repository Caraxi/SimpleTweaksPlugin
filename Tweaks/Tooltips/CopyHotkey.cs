using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState;
using Dalamud.Game.Internal;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public VK[] CopyHotkey = { VK.Ctrl, VK.C };
        public bool CopyHotkeyEnabled = true;

        public VK[] TeamcraftLinkHotkey = {VK.Ctrl, VK.T};
        public bool TeamcraftLinkHotkeyEnabled = false;

        public VK[] GardlandToolsLinkHotkey = {VK.Ctrl, VK.G};
        public bool GardlandToolsLinkHotkeyEnabled = false;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class CopyHotkey : TooltipTweaks.SubTweak {


        private readonly string weirdTabChar = Encoding.UTF8.GetString(new byte[] {0xE3, 0x80, 0x80});

        public override string Name => "Item Hotkeys";
        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, TooltipTweaks.ItemInfo itemInfo) {
            var seStr = tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay];
            if (seStr == null) return;

            var split = seStr.TextValue.Split(new string[] { weirdTabChar }, StringSplitOptions.None);
            if (split.Length > 0) {
                seStr.Payloads.Clear();
                seStr.Payloads.Add(new TextPayload(string.Join("\n", split)));
            }

            if (PluginConfig.TooltipTweaks.CopyHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", PluginConfig.TooltipTweaks.CopyHotkey.Select(k => k.GetKeyName()))}  Copy item name"));
            if (PluginConfig.TooltipTweaks.TeamcraftLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", PluginConfig.TooltipTweaks.TeamcraftLinkHotkey.Select(k => k.GetKeyName()))}  View on Teamcraft"));
            if (PluginConfig.TooltipTweaks.GardlandToolsLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", PluginConfig.TooltipTweaks.GardlandToolsLinkHotkey.Select(k => k.GetKeyName()))}  View on Garland Tools"));

            SimpleLog.Verbose(seStr.Payloads);
            tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay] = seStr;
        }

        private string settingKey = null;
        private string focused = null;
        
        private readonly List<VK> newKeys = new List<VK>();

        public void DrawHotkeyConfig(string name, ref VK[] keys, ref bool enabled, ref bool hasChanged) {
            hasChanged |= ImGui.Checkbox(name, ref enabled);
            ImGui.NextColumn();
            var strKeybind = string.Join("+", keys.Select(k => k.GetKeyName()));

            ImGui.SetNextItemWidth(100);

            if (settingKey == name) {
                for (var k = 0; k < ImGui.GetIO().KeysDown.Count && k < 160; k++) {
                    if (ImGui.GetIO().KeysDown[k]) {
                        if (!newKeys.Contains((VK)k)) {

                            if ((VK)k == VK.ESCAPE) {
                                settingKey = null;
                                newKeys.Clear();
                                focused = null;
                                break;
                            }

                            newKeys.Add((VK)k);
                            newKeys.Sort();
                        }
                    }
                }

                strKeybind = string.Join("+", newKeys.Select(k => k.GetKeyName()));
            }

            if (settingKey == name) {
                ImGui.PushStyleColor(ImGuiCol.Border, 0xFF00A5FF);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2);
            }

            ImGui.InputText($"###{GetType().Name}hotkeyDisplay{name}", ref strKeybind, 100, ImGuiInputTextFlags.ReadOnly);
            var active = ImGui.IsItemActive();
            if (settingKey == name) {

                ImGui.PopStyleColor(1);
                ImGui.PopStyleVar();

                if (focused != name) {
                    ImGui.SetKeyboardFocusHere(-1);
                    focused = name;
                } else {
                    ImGui.SameLine();
                    if (ImGui.Button(newKeys.Count > 0 ? $"Confirm##{name}" : $"Cancel##{name}")) {
                        settingKey = null;
                        if (newKeys.Count > 0) keys = newKeys.ToArray();
                        newKeys.Clear();
                        hasChanged = true;
                    } else {
                        if (!active) {
                            focused = null;
                            settingKey = null;
                            if (newKeys.Count > 0) keys = newKeys.ToArray();
                            hasChanged = true;
                            newKeys.Clear();
                        }
                    }
                }
            } else {
                ImGui.SameLine();
                if (ImGui.Button($"Set Keybind###setHotkeyButton{name}")) {
                    settingKey = name;
                }
            }
            ImGui.NextColumn();
        }

        public override void DrawConfig(ref bool hasChanged) {
            

            if (Enabled) {
                if (ImGui.TreeNode($"{this.Name}###configTreeNode")) {
                    ImGui.Columns(2);
                    ImGui.SetColumnWidth(0, 180 * ImGui.GetIO().FontGlobalScale);
                    var c = PluginConfig.TooltipTweaks;
                    DrawHotkeyConfig("Copy Item Name", ref c.CopyHotkey, ref c.CopyHotkeyEnabled, ref hasChanged);
                    ImGui.Separator();
                    DrawHotkeyConfig("View on Teamcraft", ref c.TeamcraftLinkHotkey, ref c.TeamcraftLinkHotkeyEnabled, ref hasChanged);
                    ImGui.Separator();
                    DrawHotkeyConfig("View on Garland Tools", ref c.GardlandToolsLinkHotkey, ref c.GardlandToolsLinkHotkeyEnabled, ref hasChanged);
                    ImGui.Columns();
                    ImGui.TreePop();
                }
            } else {
                base.DrawConfig(ref hasChanged);
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

        private void CopyItemName(Item item) {
            System.Windows.Forms.Clipboard.SetText(item.Name);
        }

        private void OpenTeamcraft(Item item) {
            Process.Start($"https://ffxivteamcraft.com/db//item/{item.RowId}");
        }

        private void OpenGarlandTools(Item item) {
            Process.Start($"https://www.garlandtools.org/db/#item/{item.RowId}");
        }

        private bool isHotkeyPress(VK[] keys) {
            for (var i = 0; i < 0xA0; i++) {
                if (keys.Contains((VK) i)) {
                    if (!PluginInterface.ClientState.KeyState[i]) return false;
                } else {
                    if (PluginInterface.ClientState.KeyState[i]) return false;
                }
            }
            return true;
        }

        private void FrameworkOnOnUpdateEvent(Framework framework) {
            try {
                var c = PluginConfig.TooltipTweaks;
                if (PluginInterface.Framework.Gui.HoveredItem == 0) return;

                Action<Item> action = null;
                VK[] keys = null;
                if (action == null && c.CopyHotkeyEnabled && isHotkeyPress(c.CopyHotkey)) {
                    action = CopyItemName;
                    keys = c.CopyHotkey;
                }

                if (action == null && c.TeamcraftLinkHotkeyEnabled && isHotkeyPress(c.TeamcraftLinkHotkey)) {
                    action = OpenTeamcraft;
                    keys = c.TeamcraftLinkHotkey;
                }

                if (action == null && c.GardlandToolsLinkHotkeyEnabled && isHotkeyPress(c.GardlandToolsLinkHotkey)) {
                    action = OpenGarlandTools;
                    keys = c.GardlandToolsLinkHotkey;
                }

                if (action != null) {
                    var id = PluginInterface.Framework.Gui.HoveredItem;
                    if (id >= 2000000) return;
                    id %= 500000;
                    var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);
                    if (item == null) return;
                    action(item);
                    foreach (var k in keys) {
                        PluginInterface.ClientState.KeyState[(int) k] = false;
                    }
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }
    }
}
