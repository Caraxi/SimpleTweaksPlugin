using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Item Hotkeys")]
[TweakDescription("Adds hotkeys for various actions when the item detail window is visible.")]
[TweakAutoConfig]
public unsafe class ItemHotkeys : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        public bool HideHotkeysOnTooltip;
        public bool HasLoadedOld;
    }

    public List<ItemHotkey> Hotkeys = [];

    private readonly string weirdTabChar = Encoding.UTF8.GetString([0xE3, 0x80, 0x80]);

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Config.HideHotkeysOnTooltip) return;

        var itemId = AgentItemDetail.Instance()->ItemId;

        object? item;

        if (itemId >= 2000000) {
            item = Service.Data.Excel.GetSheet<EventItem>().GetRowOrDefault(itemId);
        } else {
            item = Service.Data.Excel.GetSheet<Item>().GetRow(itemId % 500000);
        }

        if (item == null) return;

        var seStr = GetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ControlsDisplay);
        if (seStr == null) return;
        if (seStr.TextValue.Contains('\n')) return;
        var split = seStr.TextValue.Split([weirdTabChar], StringSplitOptions.None);
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

            if (itemId >= 2000000 ? hk.DoShow(item as EventItem?) : hk.DoShow(item as Item?)) {
                seStr.Payloads.Add(new TextPayload($"\n{string.Join("+", hk.Hotkey.Select(k => k.GetKeyName()))}  {hk.HintText}"));
                v++;
            }
        }

        if (v > 0) {
            try {
                SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.ControlsDisplay, seStr);
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }
    }

    public Configs Config { get; private set; }

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

        if (HotkeyHelper.DrawHotkeyConfigEditor($"###{GetType().Name}hotkeyDisplay{hotkey.Key}", hotkey.Hotkey, out var newKeys)) {
            hotkey.Hotkey = newKeys;
        }

        ImGui.SameLine();
        if (ImGui.Checkbox("Hide From Tooltip", ref hotkey.Config.HideFromTooltip)) {
            hotkey.SaveConfig();
        }

        hotkey.DrawExtraConfig();

        ImGui.PopID();
    }

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 180 * ImGui.GetIO().FontGlobalScale);
        foreach (var h in Hotkeys) {
            DrawHotkeyConfig(h);
            ImGui.Separator();
        }

        ImGui.Columns();
        ImGui.Dummy(new Vector2(5 * ImGui.GetIO().FontGlobalScale));

        hasChanged |= ImGui.Checkbox(LocString("NoHelpText", "Don't show hotkey help on Tooltip"), ref Config.HideHotkeysOnTooltip);
    }

    protected override void Setup() {
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(ItemHotkey)))) {
            var h = (ItemHotkey)Activator.CreateInstance(t);
            if (h != null) {
                Hotkeys.Add(h);
            }
        }
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        foreach (var h in Hotkeys) {
            h.Enable(true);
        }

        Common.FrameworkUpdate += OnFrameworkUpdate;
        base.Enable();
    }

    [AddonPostRequestedUpdate("ItemDetail")]
    private void AddonItemDetailOnUpdate(AtkUnitBase* atkUnitBase) {
        if (Config.HideHotkeysOnTooltip) return;
        var textNineGridComponentNode = (AtkComponentNode*)atkUnitBase->GetNodeById(3);
        if (textNineGridComponentNode == null) return;
        var textNode = (AtkTextNode*)textNineGridComponentNode->Component->UldManager.SearchNodeById(2);
        var nineGrid = (AtkNineGridNode*)textNineGridComponentNode->Component->UldManager.SearchNodeById(3);
        if (textNode == null || nineGrid == null) return;
        ushort textWidth = 0;
        ushort textHeight = 0;
        textNode->GetTextDrawSize(&textWidth, &textHeight);
        if (textHeight is 0 or > 1000) return;
        textHeight += (ushort)(textNode->AtkResNode.Y * 2);
        nineGrid->AtkResNode.SetHeight(textHeight);
    }

    private void OnFrameworkUpdate() {
        try {
            if (Service.GameGui.HoveredItem == 0) return;

            var id = Service.GameGui.HoveredItem;

            object item;
            if (id >= 2000000) {
                item = Service.Data.Excel.GetSheet<EventItem>().GetRowOrDefault((uint)id);
            } else {
                item = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault((uint)(id % 500000));
            }

            if (item == null) return;

            foreach (var h in Hotkeys) {
                if (!h.Enabled) continue;
                if (!(id >= 2000000 ? h.DoShow(item as EventItem?) : h.DoShow(item as Item?))) continue;
                
                if (!HotkeyHelper.CheckHotkeyState(h.Hotkey)) continue;
                if (id >= 2000000) {
                    h.OnTriggered(item as EventItem?);
                } else {
                    h.OnTriggered(item as Item?);
                }

                break;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    protected override void Disable() {
        foreach (var h in Hotkeys) {
            h.Disable(true);
        }

        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        foreach (var h in Hotkeys) {
            h.Dispose();
        }

        base.Dispose();
    }
}
