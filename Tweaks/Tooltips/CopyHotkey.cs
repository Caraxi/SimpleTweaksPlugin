using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Sheets;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public VK[] CopyHotkey = { VK.Ctrl, VK.C };
        public bool CopyHotkeyEnabled = false;

        public VK[] TeamcraftLinkHotkey = {VK.Ctrl, VK.T};
        public bool TeamcraftLinkHotkeyEnabled = false;
        public bool TeamcraftLinkHotkeyForceBrowser = false;

        public VK[] GardlandToolsLinkHotkey = {VK.Ctrl, VK.G};
        public bool GardlandToolsLinkHotkeyEnabled = false;

        public VK[] GamerEscapeLinkHotkey = {VK.Ctrl, VK.E};
        public bool GamerEscapeLinkHotkeyEnabled = false;

        public bool HideHotkeysOnTooltip = false;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class CopyHotkey : TooltipTweaks.SubTweak {

        public TooltipTweakConfig Config => PluginConfig.TooltipTweaks;

        private readonly string weirdTabChar = Encoding.UTF8.GetString(new byte[] {0xE3, 0x80, 0x80});

        public override string Name => "Item Hotkeys";
        public override string Description => "Adds hotkeys for various actions when the item detail window is visible.";
        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {
            if (Config.HideHotkeysOnTooltip) return;
            var seStr = tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay];
            if (seStr == null) return;

            var split = seStr.TextValue.Split(new string[] { weirdTabChar }, StringSplitOptions.None);
            if (split.Length > 0) {
                seStr.Payloads.Clear();
                seStr.Payloads.Add(new TextPayload(string.Join("\n", split)));
            }

            if (Config.CopyHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.CopyHotkey.Select(k => k.GetKeyName()))}  Copy item name"));
            if (Config.TeamcraftLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.TeamcraftLinkHotkey.Select(k => k.GetKeyName()))}  View on Teamcraft"));
            if (Config.GardlandToolsLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.GardlandToolsLinkHotkey.Select(k => k.GetKeyName()))}  View on Garland Tools"));
            if (Config.GamerEscapeLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.GamerEscapeLinkHotkey.Select(k => k.GetKeyName()))}  View on Gamer Escape"));

            SimpleLog.Verbose(seStr.Payloads);
            tooltip[TooltipTweaks.ItemTooltip.TooltipField.ControlsDisplay] = seStr;
        }

        private string settingKey = null;
        private string focused = null;
        
        private readonly List<VK> newKeys = new List<VK>();

        public void DrawHotkeyConfig(string name, ref VK[] keys, ref bool enabled, ref bool hasChanged) {
            while (ImGui.GetColumnIndex() != 0) ImGui.NextColumn();
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
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.Columns(2);
            ImGui.SetColumnWidth(0, 180 * ImGui.GetIO().FontGlobalScale);
            var c = Config;
            DrawHotkeyConfig("Copy Item Name", ref c.CopyHotkey, ref c.CopyHotkeyEnabled, ref hasChanged);
            ImGui.Separator();
            DrawHotkeyConfig("View on Teamcraft", ref c.TeamcraftLinkHotkey, ref c.TeamcraftLinkHotkeyEnabled, ref hasChanged);
            ImGui.SameLine();
            ImGui.Checkbox($"Browser Only###teamcraftIgnoreClient", ref Config.TeamcraftLinkHotkeyForceBrowser);
            ImGui.Separator();
            DrawHotkeyConfig("View on Garland Tools", ref c.GardlandToolsLinkHotkey, ref c.GardlandToolsLinkHotkeyEnabled, ref hasChanged);
            ImGui.Separator();
            DrawHotkeyConfig("View on Gamer Escape", ref c.GamerEscapeLinkHotkey, ref c.GamerEscapeLinkHotkeyEnabled, ref hasChanged);
            ImGui.Columns();
            ImGui.Dummy(new Vector2(5 * ImGui.GetIO().FontGlobalScale));
            hasChanged |= ImGui.Checkbox("Don't show hotkey help on Tooltip", ref c.HideHotkeysOnTooltip);
        };

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnOnUpdateEvent;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnOnUpdateEvent;
            base.Disable();
        }

        private void CopyItemName(ExtendedItem extendedItem) {
            ImGui.SetClipboardText(extendedItem.Name);
        }

        private bool teamcraftLocalFailed = false;

        private void OpenTeamcraft(ExtendedItem extendedItem) {
            if (teamcraftLocalFailed || Config.TeamcraftLinkHotkeyForceBrowser) {
                Process.Start($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
                return;
            }
            Task.Run(() => {
                try {
                    var wr = WebRequest.CreateHttp($"http://localhost:14500/db/en/item/{extendedItem.RowId}");
                    wr.Timeout = 500;
                    wr.Method = "GET";
                    wr.GetResponse().Close();
                } catch {
                    try {
                        if (System.IO.Directory.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffxiv-teamcraft"))) {
                            Process.Start($"teamcraft:///db/en/item/{extendedItem.RowId}");
                        } else {
                            teamcraftLocalFailed = true;
                            Process.Start($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
                        }
                    } catch {
                        teamcraftLocalFailed = true;
                        Process.Start($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
                    }
                }
            });
        }

        private void OpenGarlandTools(ExtendedItem extendedItem) {
            Process.Start($"https://www.garlandtools.org/db/#item/{extendedItem.RowId}");
        }
        
        private void OpenGamerEscape(ExtendedItem extendedItem) {
            var name = Uri.EscapeUriString(extendedItem.Name);
            Process.Start($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
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
                if (PluginInterface.Framework.Gui.HoveredItem == 0) return;

                Action<ExtendedItem> action = null;
                VK[] keys = null;

                var language = PluginInterface.ClientState.ClientLanguage;
                if (action == null && Config.CopyHotkeyEnabled && isHotkeyPress(Config.CopyHotkey)) {
                    action = CopyItemName;
                    keys = Config.CopyHotkey;
                }

                if (action == null && Config.TeamcraftLinkHotkeyEnabled && isHotkeyPress(Config.TeamcraftLinkHotkey)) {
                    action = OpenTeamcraft;
                    keys = Config.TeamcraftLinkHotkey;
                }

                if (action == null && Config.GardlandToolsLinkHotkeyEnabled && isHotkeyPress(Config.GardlandToolsLinkHotkey)) {
                    action = OpenGarlandTools;
                    keys = Config.GardlandToolsLinkHotkey;
                }
                
                if (action == null && Config.GamerEscapeLinkHotkeyEnabled && isHotkeyPress(Config.GamerEscapeLinkHotkey)) {
                    action = OpenGamerEscape;
                    keys = Config.GamerEscapeLinkHotkey;
                    language = ClientLanguage.English;
                }

                if (action != null) {
                    var id = PluginInterface.Framework.Gui.HoveredItem;
                    if (id >= 2000000) return;
                    id %= 500000;
                    var item = PluginInterface.Data.GetExcelSheet<ExtendedItem>(language).GetRow((uint) id);
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
