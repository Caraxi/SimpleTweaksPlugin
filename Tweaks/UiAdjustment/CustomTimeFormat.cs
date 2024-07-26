using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Config;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Custom Time Formats")]
[TweakDescription("Allows setting custom time formats for the in game clock. Uses C# formatting strings.")]
[TweakAutoConfig]
[Changelog("1.8.8.0", "Fixed tooltip when hovering clocks")]
[Changelog("1.8.8.0", "Returned 'click to change clock' feature from base game.")]
public unsafe class CustomTimeFormat : UiAdjustments.SubTweak {
    public class Config : TweakConfig {
        public string CustomFormatET = "HH:mm:ss";
        public string CustomFormatLT = "HH:mm:ss";
        public string CustomFormatST = "HH:mm:ss";

        public int[] Order = { 0, 1, 2 };
    }

    public Config TweakConfig { get; private set; }

    private float maxX;

    [AddonPreDraw("_DTR")]
    private void UpdateAddon(AtkUnitBase* atkUnitBase) {
        try {
            var clockButtonNode = atkUnitBase->GetNodeById(16);
            if (clockButtonNode == null) return;
            if ((ushort)clockButtonNode->Type < 1000) return;
            var clockButtonComponent = clockButtonNode->GetComponent();
            if (clockButtonComponent == null) return;
            var buttonTextNode = (AtkTextNode*)clockButtonComponent->GetTextNodeById(2);
            if (buttonTextNode == null) return;
            UpdateTimeString(&buttonTextNode->NodeText);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private class MoveAction {
        public int Index;
        public bool MoveUp;
    }

    private void DrawClockConfig(int index, string name, string icon, ref bool hasChanged, ref bool enabled, ref string format, ref MoveAction moveAction, DateTimeOffset example) {
        ImGui.Text(icon);
        ImGui.SameLine();

        // Reordering
        ImGui.SetWindowFontScale(1.3f);
        var p2 = ImGui.GetCursorPos();
        ImGui.PushFont(UiBuilder.IconFont);
        var white = new Vector4(1, 1, 1, 1);
        var other = new Vector4(1, 1, 0, 1);
        var up = $"{(char)FontAwesomeIcon.SortUp}";
        var down = $"{(char)FontAwesomeIcon.SortDown}";
        var s = ImGui.CalcTextSize(up);

        ImGui.BeginGroup();
        var p3 = ImGui.GetCursorPos();
        var p4 = ImGui.GetCursorScreenPos();
        var hoveringUp = ImGui.IsMouseHoveringRect(p4, p4 + new Vector2(s.X, s.Y / 2));
        var hoveringDown = !hoveringUp && ImGui.IsMouseHoveringRect(p4 + new Vector2(0, s.Y / 2), p4 + s);

        if (index > 0) {
            ImGui.TextColored(hoveringUp ? other : white, up);
            if (hoveringUp && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                moveAction = new MoveAction() { Index = index, MoveUp = true };
            }
        }

        ImGui.SetCursorPos(p3);
        if (index < 2) {
            ImGui.TextColored(hoveringDown ? other : white, down);
            if (hoveringDown && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                moveAction = new MoveAction() { Index = index, MoveUp = false };
            }
        }

        ImGui.EndGroup();
        ImGui.SetCursorPos(p2 + new Vector2(s.X, 0));
        ImGui.PopFont();
        ImGui.SetWindowFontScale(1.0f);
        // End Reordering

        hasChanged |= ImGui.Checkbox($"###enable{name}", ref enabled);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        hasChanged |= ImGui.InputText(name + "##formatEditInput", ref format, 50);

        ImGui.SameLine();
        if (ImGui.GetCursorPosX() > maxX) maxX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(maxX);
        try {
            var preview = $"{example.DateTime.ToString(format)}";
            ImGui.SetNextItemWidth(120);
            ImGui.InputText($"###preview{name}", ref preview, 50, ImGuiInputTextFlags.ReadOnly);
        } catch {
            ImGui.Text("Format Invalid");
        }
    }

    private bool DrawClockConfig(int id, int index, string[] icons, ref bool hasChanged, ref MoveAction moveAction) {
        switch (id) {
            case 0: {
                if (Service.GameConfig.TryGet(UiConfigOption.TimeEorzea, out bool showET)) {
                    var et = DateTimeOffset.FromUnixTimeSeconds(FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientTime.EorzeaTime);
                    DrawClockConfig(index, LocString("Eorzea Time"), icons[0], ref hasChanged, ref showET, ref TweakConfig.CustomFormatET, ref moveAction, et);
                    if (hasChanged) Service.GameConfig.Set(UiConfigOption.TimeEorzea, showET);
                } else {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Error: Failed to get ET config.");
                }

                break;
            }
            case 1: {
                if (Service.GameConfig.TryGet(UiConfigOption.TimeLocal, out bool showLT)) {
                    DrawClockConfig(index, LocString("Local Time"), icons[1], ref hasChanged, ref showLT, ref TweakConfig.CustomFormatLT, ref moveAction, DateTimeOffset.Now);
                    if (hasChanged) Service.GameConfig.Set(UiConfigOption.TimeLocal, showLT);
                } else {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Error: Failed to get LT config.");
                }

                break;
            }
            case 2: {
                if (Service.GameConfig.TryGet(UiConfigOption.TimeServer, out bool showST)) {
                    DrawClockConfig(index, LocString("Server Time"), icons[2], ref hasChanged, ref showST, ref TweakConfig.CustomFormatST, ref moveAction, DateTimeOffset.UtcNow);
                    if (hasChanged) Service.GameConfig.Set(UiConfigOption.TimeServer, showST);
                } else {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Error: Failed to get ST config.");
                }

                break;
            }
            default: {
                // Broken
                TweakConfig.Order = new[] { 0, 1, 2 };
                SimpleLog.Error("Broken Config Detected. Automatically Fixed");
                hasChanged = true;
                return false;
            }
        }

        return true;
    }

    protected void DrawConfig(ref bool hasChanged) {
        var icons = GetClockIcons();

        // Safety
        var order = new[] { -1, -1, -1 };
        if (TweakConfig.Order.Length != 3) {
            TweakConfig.Order = new[] { 0, 1, 2 };
            SimpleLog.Error("Broken Config Detected. Automatically Fixed");
            hasChanged = true;
        }

        for (var i = 0; i < TweakConfig.Order.Length; i++) {
            order[i] = TweakConfig.Order[i];
        }

        if (!(order.Contains(0) && order.Contains(1) && order.Contains(2))) {
            order = new[] { 0, 1, 2 };
            TweakConfig.Order = new[] { 0, 1, 2 };
            SimpleLog.Error("Broken Config Detected. Automatically Fixed");
            hasChanged = true;
        }

        MoveAction moveAction = null;
        for (var i = 0; i < order.Length; i++) {
            if (!DrawClockConfig(order[i], i, icons, ref hasChanged, ref moveAction)) {
                break;
            }
        }

        if (moveAction != null) {
            if (moveAction.MoveUp) {
                if (moveAction.Index > 0) {
                    var moving = TweakConfig.Order[moveAction.Index];
                    var replacing = TweakConfig.Order[moveAction.Index - 1];
                    TweakConfig.Order[moveAction.Index - 1] = moving;
                    TweakConfig.Order[moveAction.Index] = replacing;
                    hasChanged = true;
                }
            } else {
                if (moveAction.Index < 2) {
                    var moving = TweakConfig.Order[moveAction.Index];
                    var replacing = TweakConfig.Order[moveAction.Index + 1];
                    TweakConfig.Order[moveAction.Index + 1] = moving;
                    TweakConfig.Order[moveAction.Index] = replacing;
                    hasChanged = true;
                }
            }
        }
    }

    private string[] GetClockIcons() =>
        Service.ClientState.ClientLanguage switch {
            ClientLanguage.German => [$"{(char)SeIconChar.EorzeaTimeDe}", $"{(char)SeIconChar.LocalTimeDe}", $"{(char)SeIconChar.ServerTimeDe}"],
            ClientLanguage.French => [$"{(char)SeIconChar.EorzeaTimeFr}", $"{(char)SeIconChar.LocalTimeFr}", $"{(char)SeIconChar.ServerTimeFr}"],
            _ => [$"{(char)SeIconChar.EorzeaTimeEn}", $"{(char)SeIconChar.LocalTimeEn}", $"{(char)SeIconChar.ServerTimeEn}"],
        };

    private unsafe void UpdateTimeString(Utf8String* xivString) {
        if (!(Service.GameConfig.TryGet(UiConfigOption.TimeEorzea, out bool showET) && Service.GameConfig.TryGet(UiConfigOption.TimeLocal, out bool showLT) && Service.GameConfig.TryGet(UiConfigOption.TimeServer, out bool showST))) return;

        var icons = GetClockIcons();
        var et = DateTimeOffset.FromUnixTimeSeconds(FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientTime.EorzeaTime);
        var lt = DateTimeOffset.Now;
        var timeSeString = new SeString(new List<Payload>());

        try {
            foreach (var c in TweakConfig.Order) {
                switch (c) {
                    case 0: {
                        if (showET)
                            timeSeString.Payloads.Add(new TextPayload($"{icons[0]} {et.DateTime.ToString(TweakConfig.CustomFormatET)} "));
                        break;
                    }
                    case 1: {
                        if (showLT)
                            timeSeString.Payloads.Add(new TextPayload($"{icons[1]} {lt.DateTime.ToString(TweakConfig.CustomFormatLT)} "));
                        break;
                    }
                    case 2: {
                        if (showST)
                            timeSeString.Payloads.Add(new TextPayload($"{icons[2]} {lt.UtcDateTime.ToString(TweakConfig.CustomFormatST)} "));
                        break;
                    }
                }
            }
        } catch {
            timeSeString.Payloads.Add(new TextPayload("Invalid Time Format"));
        }

        if (timeSeString.Payloads.Count > 0) {
            xivString->SetString(timeSeString.Encode());
        }
    }
}
