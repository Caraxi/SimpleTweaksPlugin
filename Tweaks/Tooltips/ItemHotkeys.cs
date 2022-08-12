using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class ItemHotkeys : TooltipTweaks.SubTweak {
    
    public class OldHotkeyConfig : TweakConfig {
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
        
        public VirtualKey[] OpenRecipeHotkey = {VirtualKey.CONTROL, VirtualKey.R};
        public bool OpenRecipeEnabled;
        
        public bool HideHotkeysOnTooltip;
    }
    
    public override string Name => "Item Hotkeys";
    public override string Description => "Adds hotkeys for various actions when the item detail window is visible.";

    public class Configs : TweakConfig {
        public bool HideHotkeysOnTooltip;
        public bool HasLoadedOld;
    }

    public List<ItemHotkey> Hotkeys = new();

    private readonly string weirdTabChar = Encoding.UTF8.GetString(new byte[] {0xE3, 0x80, 0x80});
    
    private HookWrapper<Common.AddonOnUpdate> itemDetailOnUpdateHook;
    
    
    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Config.HideHotkeysOnTooltip) return;

        var itemId = Service.GameGui.HoveredItem;

        ExcelRow item;

        if (itemId >= 2000000) {
            item = Service.Data.Excel.GetSheet<EventItem>()?.GetRow((uint)Service.GameGui.HoveredItem);
        } else {
            item = Service.Data.Excel.GetSheet<ExtendedItem>()?.GetRow((uint)(Service.GameGui.HoveredItem % 500000));
        }
        
        if (item == null) return;
        
        var seStr = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ControlsDisplay);
        if (seStr == null) return;
        if (seStr.TextValue.Contains('\n')) return;
        var split = seStr.TextValue.Split(new[] { weirdTabChar }, StringSplitOptions.None);
        if (split.Length > 0) {
            seStr.Payloads.Clear();
            seStr.Payloads.Add(new TextPayload(string.Join("\n", split)));
        }

        var v = 0;
        
        foreach (var hk in Hotkeys) {
            if (!hk.Enabled) continue;
            if (hk.Config.HideFromTooltip) continue;

            if (itemId >= 2000000) {
                if (!hk.AcceptsEventItem) continue;
            } else {
                if (!hk.AcceptsNormalItem) continue;
            }

            if (itemId >= 2000000 ? hk.DoShow(item as EventItem) : hk.DoShow(item as ExtendedItem)) {
                seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", hk.Hotkey.Select(k => k.GetKeyName()))}  {hk.HintText}"));
                v++;
            }
        }
        
        if (v > 0) {
            SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ControlsDisplay, seStr);
        }
    }

    public Configs Config { get; private set; }



    private string settingKey;
    private string focused;
    private readonly List<VirtualKey> newKeys = new();
    
    public void DrawHotkeyConfig(ItemHotkey hotkey) {
        ImGui.PushID(hotkey.Key);
        while (ImGui.GetColumnIndex() != 0) ImGui.NextColumn();

        var enabled = hotkey.Enabled;

        if (ImGui.Checkbox(hotkey.LocalizedName, ref enabled)) {
            if (enabled) {
                hotkey.Enable();
            } else {
                hotkey.Disable();
            }
        }
        ImGui.NextColumn();
        var strKeybind = string.Join("+", hotkey.Hotkey.Select(k => k.GetKeyName()));

        ImGui.SetNextItemWidth(100);

        if (settingKey == hotkey.Key) {
            if (ImGui.GetIO().KeyAlt && !newKeys.Contains(VirtualKey.MENU)) newKeys.Add(VirtualKey.MENU);
            if (ImGui.GetIO().KeyShift && !newKeys.Contains(VirtualKey.SHIFT)) newKeys.Add(VirtualKey.SHIFT);
            if (ImGui.GetIO().KeyCtrl && !newKeys.Contains(VirtualKey.CONTROL)) newKeys.Add(VirtualKey.CONTROL);
            
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
                    }
                }
            }

            newKeys.Sort();
            strKeybind = string.Join("+", newKeys.Select(k => k.GetKeyName()));
        }

        if (settingKey == hotkey.Key) {
            ImGui.PushStyleColor(ImGuiCol.Border, 0xFF00A5FF);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2);
        }

        ImGui.InputText($"###{GetType().Name}hotkeyDisplay{hotkey.Key}", ref strKeybind, 100, ImGuiInputTextFlags.ReadOnly);
        var active = ImGui.IsItemActive();
        if (settingKey == hotkey.Key) {

            ImGui.PopStyleColor(1);
            ImGui.PopStyleVar();

            if (focused != hotkey.Key) {
                ImGui.SetKeyboardFocusHere(-1);
                focused = hotkey.Key;
            } else {
                ImGui.SameLine();
                if (ImGui.Button(newKeys.Count > 0 ? LocString("Confirm") + $"##{hotkey.Key}" : LocString("Cancel") + $"##{hotkey.Key}")) {
                    settingKey = null;
                    if (newKeys.Count > 0) hotkey.Hotkey = newKeys.ToArray();
                    newKeys.Clear();
                } else {
                    if (!active) {
                        focused = null;
                        settingKey = null;
                        if (newKeys.Count > 0) hotkey.Hotkey = newKeys.ToArray();
                        newKeys.Clear();
                    }
                }
            }
        } else {
            ImGui.SameLine();
            if (ImGui.Button(LocString("Set Keybind") + $"###setHotkeyButton{hotkey.Key}")) {
                settingKey = hotkey.Key;
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Checkbox("Hide From Tooltip", ref hotkey.Config.HideFromTooltip)) {
            hotkey.SaveConfig();
        }
        
        
        hotkey.DrawExtraConfig();
        
        ImGui.PopID();
    }
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {


        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 180 * ImGui.GetIO().FontGlobalScale);
        foreach (var h in Hotkeys) {
            DrawHotkeyConfig(h);
            ImGui.Separator();
        }
        
        
        ImGui.Columns();
        ImGui.Dummy(new Vector2(5 * ImGui.GetIO().FontGlobalScale));

        hasChanged |= ImGui.Checkbox(LocString("NoHelpText", "Don't show hotkey help on Tooltip"), ref Config.HideHotkeysOnTooltip);
    };

    public override void Setup() {

        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(ItemHotkey)))) {
            var h = (ItemHotkey) Activator.CreateInstance(t);
            if (h != null) {
                Hotkeys.Add(h);
            }
            
        }

        base.Setup();
    }

    public override void Enable() {

        Config = LoadConfig<Configs>() ?? new Configs();

        try {
            if (!Config.HasLoadedOld) {
                var old = LoadConfig<OldHotkeyConfig>("TooltipTweaks@CopyHotkey");
                if (old == null) goto SkipConfigLoad;

                ItemHotkey SetupFromOld(Type t, bool enabled, VirtualKey[] keys) {
                    var k = Hotkeys.First(h => h.GetType() == t);
                    if (k.GetType() != t) return null;
                    k.LoadConfig();
                    k.Config.Enabled = enabled;
                    k.Config.Key = keys;
                    k.SaveConfig();
                    return k;
                }

                SetupFromOld(typeof(CopyNameHotkey), old.CopyHotkeyEnabled, old.CopyHotkey);
                SetupFromOld(typeof(ViewOnUniversalis), old.UniversalisHotkeyEnabled, old.UniversalisHotkey);
                SetupFromOld(typeof(ViewOnGamerEscape), old.GamerEscapeLinkHotkeyEnabled, old.GamerEscapeLinkHotkey);
                SetupFromOld(typeof(ViewOnGarlandTools), old.GardlandToolsLinkHotkeyEnabled, old.GardlandToolsLinkHotkey);
                SetupFromOld(typeof(ViewOnGarlandTools), old.GardlandToolsLinkHotkeyEnabled, old.GardlandToolsLinkHotkey);
                (SetupFromOld(typeof(ViewOnTeamcraft), old.TeamcraftLinkHotkeyEnabled, old.TeamcraftLinkHotkey).Config as ViewOnTeamcraft.HotkeyConfig)!.ForceBrowser = old.TeamcraftLinkHotkeyForceBrowser;

                SkipConfigLoad:
                Config.HasLoadedOld = true;
                SaveConfig(Config);
            }
        } catch {
            Config.HasLoadedOld = true;
            SaveConfig(Config);
        }
        
        
        foreach (var h in Hotkeys) {
            h.Enable(true);
        }
        
        
        
        
        
        Common.FrameworkUpdate += OnFrameworkUpdate;

        itemDetailOnUpdateHook ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B AA", AddonItemDetailOnUpdate);
        itemDetailOnUpdateHook?.Enable();
        
        

        base.Enable();
    }

    private void AddonItemDetailOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings) {
        if (Config.HideHotkeysOnTooltip) return;
        var textNineGridComponentNode = (AtkComponentNode*) atkUnitBase->GetNodeById(3);
        if (textNineGridComponentNode == null) return;
        var textNode = (AtkTextNode*) textNineGridComponentNode->Component->UldManager.SearchNodeById(2);
        var nineGrid = (AtkNineGridNode*) textNineGridComponentNode->Component->UldManager.SearchNodeById(3);
        if (textNode == null || nineGrid == null) return;
        ushort textWidth = 0;
        ushort textHeight = 0;
        textNode->GetTextDrawSize(&textWidth, &textHeight);
        if (textHeight is 0 or > 1000) return;
        textHeight += (ushort)(textNode->AtkResNode.Y * 2);
        nineGrid->AtkResNode.SetHeight(textHeight);
    }

    private bool CheckHotkeyState(VirtualKey[] keys) {
        foreach (var vk in Service.KeyState.GetValidVirtualKeys()) {
            if (keys.Contains(vk)) {
                if (!Service.KeyState[vk]) return false;
            } else {
                if (Service.KeyState[vk]) return false;
            }
        }
        return true;
    }
    
    private void OnFrameworkUpdate() {
        try {
            if (Service.GameGui.HoveredItem == 0) return;
            
            var id = Service.GameGui.HoveredItem;

            ExcelRow item;
            if (id >= 2000000) {
                item = Service.Data.Excel.GetSheet<EventItem>()?.GetRow((uint)id);
            } else {
                item = Service.Data.Excel.GetSheet<ExtendedItem>()?.GetRow((uint) (id % 500000)) ;
            }

            if (item == null) return;
            
            foreach (var h in Hotkeys) {
                if (!h.Enabled) continue;
                if (!(id >= 2000000 ? h.DoShow(item as EventItem) : h.DoShow(item as ExtendedItem))) continue;
                if (CheckHotkeyState(h.Hotkey)) {
                    if (id >= 2000000) {
                        h.OnTriggered(item as EventItem);
                    } else {
                        h.OnTriggered(item as ExtendedItem);
                    }
                    foreach (var k in h.Hotkey) {
                        Service.KeyState[(int) k] = false;
                    }
                    break;
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        
        
    }

    public override void Disable() {
        foreach (var h in Hotkeys) {
            h.Disable(true);
        }
        
        SaveConfig(Config);
        itemDetailOnUpdateHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        itemDetailOnUpdateHook?.Dispose();

        foreach (var h in Hotkeys) {
            h.Dispose();
        }
        
        base.Dispose();
    }
}

