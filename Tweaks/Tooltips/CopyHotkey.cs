using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class CopyHotkey : TooltipTweaks.SubTweak {

        public class Configs : TweakConfig {
            public VirtualKey[] CopyHotkey = { VirtualKey.CONTROL, VirtualKey.C };
            public bool CopyHotkeyEnabled = false;

            public VirtualKey[] TeamcraftLinkHotkey = {VirtualKey.CONTROL, VirtualKey.T};
            public bool TeamcraftLinkHotkeyEnabled = false;
            public bool TeamcraftLinkHotkeyForceBrowser = false;

            public VirtualKey[] GardlandToolsLinkHotkey = {VirtualKey.CONTROL, VirtualKey.G};
            public bool GardlandToolsLinkHotkeyEnabled = false;

            public VirtualKey[] GamerEscapeLinkHotkey = {VirtualKey.CONTROL, VirtualKey.E};
            public bool GamerEscapeLinkHotkeyEnabled = false;

            public bool HideHotkeysOnTooltip = false;
        }
        
        public Configs Config { get; private set; }

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
        
        private readonly List<VirtualKey> newKeys = new List<VirtualKey>();

        public void DrawHotkeyConfig(string name, ref VirtualKey[] keys, ref bool enabled, ref bool hasChanged) {
            while (ImGui.GetColumnIndex() != 0) ImGui.NextColumn();
            hasChanged |= ImGui.Checkbox(name, ref enabled);
            ImGui.NextColumn();
            var strKeybind = string.Join("+", keys.Select(k => k.GetKeyName()));

            ImGui.SetNextItemWidth(100);

            if (settingKey == name) {
                for (var k = 0; k < ImGui.GetIO().KeysDown.Count && k < 160; k++) {
                    if (ImGui.GetIO().KeysDown[k]) {
                        if (!newKeys.Contains((VirtualKey)k)) {

                            if ((VirtualKey)k == VirtualKey.ESCAPE) {
                                settingKey = null;
                                newKeys.Clear();
                                focused = null;
                                break;
                            }

                            newKeys.Add((VirtualKey)k);
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

        private ExcelSheet<ExtendedItem> itemSheet;

        public override void Enable() {
            this.itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
            if (itemSheet == null) return;
            Config = LoadConfig<Configs>() ?? new Configs();
            Service.Framework.Update += FrameworkOnOnUpdateEvent;
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            Service.Framework.Update -= FrameworkOnOnUpdateEvent;
            base.Disable();
        }

        private void CopyItemName(ExtendedItem extendedItem) {
            ImGui.SetClipboardText(extendedItem.Name);
        }

        private bool teamcraftLocalFailed = false;

        private void OpenTeamcraft(ExtendedItem extendedItem) {
            if (teamcraftLocalFailed || Config.TeamcraftLinkHotkeyForceBrowser) {
                Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
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
                            Common.OpenBrowser($"teamcraft:///db/en/item/{extendedItem.RowId}");
                        } else {
                            teamcraftLocalFailed = true;
                            Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
                        }
                    } catch {
                        teamcraftLocalFailed = true;
                        Common.OpenBrowser($"https://ffxivteamcraft.com/db/en/item/{extendedItem.RowId}");
                    }
                }
            });
        }

        private void OpenGarlandTools(ExtendedItem extendedItem) {
            Common.OpenBrowser($"https://www.garlandtools.org/db/#item/{extendedItem.RowId}");
        }
        
        private void OpenGamerEscape(ExtendedItem extendedItem) {
            var name = Uri.EscapeUriString(extendedItem.Name);
            Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
        }

        private bool isHotkeyPress(VirtualKey[] keys) {
            foreach (var vk in Service.KeyState.GetValidVirtualKeys()) {
                if (keys.Contains(vk)) {
                    if (!Service.KeyState[vk]) return false;
                } else {
                    if (Service.KeyState[vk]) return false;
                }
            }
            return true;
        }

        private void FrameworkOnOnUpdateEvent(Framework framework) {
            try {
                if (Service.GameGui.HoveredItem == 0) return;

                Action<ExtendedItem> action = null;
                VirtualKey[] keys = null;

                var language = Service.ClientState.ClientLanguage;
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
                    var id = Service.GameGui.HoveredItem;
                    if (id >= 2000000) return;
                    id %= 500000;
                    var item = itemSheet.GetRow((uint) id);
                    if (item == null) return;
                    action(item);
                    foreach (var k in keys) {
                        Service.KeyState[(int) k] = false;
                    }
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }
    }
}
