using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public unsafe class CopyHotkey : TooltipTweaks.SubTweak {

    public class Configs : TweakConfig {
        public VirtualKey[] CopyHotkey = { VirtualKey.CONTROL, VirtualKey.C };
        public bool CopyHotkeyEnabled;

        public VirtualKey[] TeamcraftLinkHotkey = {VirtualKey.CONTROL, VirtualKey.T};
        public bool TeamcraftLinkHotkeyEnabled;
        public bool TeamcraftLinkHotkeyForceBrowser;

        public VirtualKey[] GardlandToolsLinkHotkey = {VirtualKey.CONTROL, VirtualKey.G};
        public bool GardlandToolsLinkHotkeyEnabled;

        public VirtualKey[] GamerEscapeLinkHotkey = {VirtualKey.CONTROL, VirtualKey.E};
        public bool GamerEscapeLinkHotkeyEnabled;
            
        public VirtualKey[] UniversalisHotkey = {VirtualKey.CONTROL, VirtualKey.U};
        public bool UniversalisHotkeyEnabled;
        
        public VirtualKey[] ErionesLinkHotkey = {VirtualKey.SHIFT, VirtualKey.E};
        public bool ErionesLinkHotkeyEnabled;

        public bool HideHotkeysOnTooltip;
    }
        
    public Configs Config { get; private set; }

    private readonly string weirdTabChar = Encoding.UTF8.GetString(new byte[] {0xE3, 0x80, 0x80});

    public override string Name => "Item Hotkeys";
    public override string Description => "Adds hotkeys for various actions when the item detail window is visible.";

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Config.HideHotkeysOnTooltip) return;
        var seStr = GetTooltipString(stringArrayData, ControlsDisplay);
        if (seStr == null) return;
        if (seStr.TextValue.Contains('\n')) return;
        var split = seStr.TextValue.Split(new[] { weirdTabChar }, StringSplitOptions.None);
        if (split.Length > 0) {
            seStr.Payloads.Clear();
            seStr.Payloads.Add(new TextPayload(string.Join("\n", split)));
        }

        
        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow((uint)(Service.GameGui.HoveredItem % 500000));

        if (Config.CopyHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.CopyHotkey.Select(k => k.GetKeyName()))}  Copy item name"));
        if (Config.TeamcraftLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.TeamcraftLinkHotkey.Select(k => k.GetKeyName()))}  View on Teamcraft"));
        if (Config.GardlandToolsLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.GardlandToolsLinkHotkey.Select(k => k.GetKeyName()))}  View on Garland Tools"));
        if (Config.GamerEscapeLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.GamerEscapeLinkHotkey.Select(k => k.GetKeyName()))}  View on Gamer Escape"));
        if (Config.UniversalisHotkeyEnabled && (Service.GameGui.HoveredItem > 0 && Service.GameGui.HoveredItem < 2000000) && Service.Data.Excel.GetSheet<Item>()?.GetRow((uint)(Service.GameGui.HoveredItem % 500000))?.ItemSearchCategory.Row > 0) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.UniversalisHotkey.Select(k => k.GetKeyName()))}  View on Universalis"));
        if (Config.ErionesLinkHotkeyEnabled) seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", Config.ErionesLinkHotkey.Select(k => k.GetKeyName()))}  View on Eriones (JP)"));


        SetTooltipString(stringArrayData, ControlsDisplay, seStr);
    }

    private string settingKey;
    private string focused;
        
    private readonly List<VirtualKey> newKeys = new();

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
                if (ImGui.Button(newKeys.Count > 0 ? LocString("Confirm") + $"##{name}" : LocString("Cancel") + $"##{name}")) {
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
            if (ImGui.Button(LocString("Set Keybind") + $"###setHotkeyButton{name}")) {
                settingKey = name;
            }
        }
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 180 * ImGui.GetIO().FontGlobalScale);
        var c = Config;
        DrawHotkeyConfig(LocString("Copy Item Name"), ref c.CopyHotkey, ref c.CopyHotkeyEnabled, ref hasChanged);
        ImGui.Separator();
        DrawHotkeyConfig(LocString("View on Teamcraft"), ref c.TeamcraftLinkHotkey, ref c.TeamcraftLinkHotkeyEnabled, ref hasChanged);
        ImGui.SameLine();
        ImGui.Checkbox(LocString("Browser Only") + "###teamcraftIgnoreClient", ref Config.TeamcraftLinkHotkeyForceBrowser);
        ImGui.Separator();
        DrawHotkeyConfig(LocString("View on Garland Tools"), ref c.GardlandToolsLinkHotkey, ref c.GardlandToolsLinkHotkeyEnabled, ref hasChanged);
        ImGui.Separator();
        DrawHotkeyConfig(LocString("View on Gamer Escape"), ref c.GamerEscapeLinkHotkey, ref c.GamerEscapeLinkHotkeyEnabled, ref hasChanged);
        ImGui.Separator();
        DrawHotkeyConfig(LocString("View on Universalis"), ref c.UniversalisHotkey, ref c.UniversalisHotkeyEnabled, ref hasChanged);
        ImGui.Separator();
        DrawHotkeyConfig(LocString("View on Eriones (JP)"), ref c.ErionesLinkHotkey, ref c.ErionesLinkHotkeyEnabled, ref hasChanged);
        ImGui.Columns();
        ImGui.Dummy(new Vector2(5 * ImGui.GetIO().FontGlobalScale));
        hasChanged |= ImGui.Checkbox(LocString("NoHelpText", "Don't show hotkey help on Tooltip"), ref c.HideHotkeysOnTooltip);
    };

    private ExcelSheet<ExtendedItem> itemSheet;

    private HookWrapper<Common.AddonOnUpdate> itemDetailOnUpdateHook;

    public override void Enable() {
        this.itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
        if (itemSheet == null) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += FrameworkOnOnUpdateEvent;

        itemDetailOnUpdateHook ??= Common.Hook<Common.AddonOnUpdate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", AddonItemDetailOnUpdate);
        itemDetailOnUpdateHook?.Enable();

        base.Enable();
    }

    private void* AddonItemDetailOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings) {
        var ret = itemDetailOnUpdateHook.Original(atkUnitBase, nums, strings);
        if (Config.HideHotkeysOnTooltip) return ret;
        var textNineGridComponentNode = (AtkComponentNode*) atkUnitBase->GetNodeById(3);
        if (textNineGridComponentNode == null) return ret;
        var textNode = (AtkTextNode*) textNineGridComponentNode->Component->UldManager.SearchNodeById(2);
        var nineGrid = (AtkNineGridNode*) textNineGridComponentNode->Component->UldManager.SearchNodeById(3);
        if (textNode == null || nineGrid == null) return ret;
        ushort textWidth = 0;
        ushort textHeight = 0;
        textNode->GetTextDrawSize(&textWidth, &textHeight);
        if (textHeight is 0 or > 1000) return ret;
        textHeight += (ushort)(textNode->AtkResNode.Y * 2);
        nineGrid->AtkResNode.SetHeight(textHeight);
        return ret;
    }

    public override void Disable() {
        SaveConfig(Config);
        itemDetailOnUpdateHook?.Disable();
        Service.Framework.Update -= FrameworkOnOnUpdateEvent;
        base.Disable();
    }

    public override void Dispose() {
        itemDetailOnUpdateHook?.Dispose();
        base.Dispose();
    }

    private void CopyItemName(ExtendedItem extendedItem) {
        ImGui.SetClipboardText(extendedItem.Name);
    }

    private bool teamcraftLocalFailed;

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
    
    private void OpenUniversalis(ExtendedItem extendedItem) {
        Common.OpenBrowser($"https://universalis.app/market/{extendedItem.RowId}");
    }
        
    private void OpenGamerEscape(ExtendedItem extendedItem) {
        var enItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.English)?.GetRow(extendedItem.RowId);
        if (enItem == null) return;
        var name = Uri.EscapeUriString(enItem.Name);
        Common.OpenBrowser($"https://ffxiv.gamerescape.com/w/index.php?search={name}");
    }

    private void OpenEriones(ExtendedItem extendedItem) {
        var jpItem = Service.Data.Excel.GetSheet<ExtendedItem>(Language.Japanese)?.GetRow(extendedItem.RowId);
        if (jpItem == null) return;
        var name = Uri.EscapeUriString(jpItem.Name);
        Common.OpenBrowser($"https://eriones.com/search?i={name}");
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

            var id = Service.GameGui.HoveredItem;
            if (id >= 2000000) return;
            id %= 500000;
            var item = itemSheet.GetRow((uint) id);
            if (item == null) return;
            
            if (Config.CopyHotkeyEnabled && isHotkeyPress(Config.CopyHotkey)) {
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
            }
            
            if (action == null && item.ItemSearchCategory.Row != 0 && Config.UniversalisHotkeyEnabled && isHotkeyPress(Config.UniversalisHotkey)) {
                action = OpenUniversalis;
                keys = Config.UniversalisHotkey;
            }

            if (action == null && Config.ErionesLinkHotkeyEnabled && isHotkeyPress(Config.ErionesLinkHotkey)) {
                action = OpenEriones;
                keys = Config.ErionesLinkHotkey;
            }

            if (action != null) {
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