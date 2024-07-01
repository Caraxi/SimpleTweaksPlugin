﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud;
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
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public bool ShouldSerializeCustomTimeFormats() => CustomTimeFormats != null;
        public CustomTimeFormat.Config CustomTimeFormats = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class CustomTimeFormat : UiAdjustments.SubTweak {
        public class Config : TweakConfig {
            public string CustomFormatET = "HH:mm:ss";
            public string CustomFormatLT = "HH:mm:ss";
            public string CustomFormatST = "HH:mm:ss";

            public int[] Order = { 0, 1, 2 };
        }

        public Config TweakConfig { get; private set; }
        
        private float maxX;

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
                    moveAction = new MoveAction() {Index = index, MoveUp = true};
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

        private unsafe bool DrawClockConfig(int id, int index, string[] icons, ref bool hasChanged, ref MoveAction moveAction) {
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
                    TweakConfig.Order = new[] {0, 1, 2};
                    SimpleLog.Error("Broken Config Detected. Automatically Fixed");
                    hasChanged = true;
                    return false;
                }
            }

            return true;
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            var icons = GetClockIcons();
            
            // Safety
            var order = new[] { -1, -1, -1};
            if (TweakConfig.Order.Length != 3) {
                TweakConfig.Order = new[] { 0, 1, 2};
                SimpleLog.Error("Broken Config Detected. Automatically Fixed");
                hasChanged = true;
            }
            for (var i = 0; i < TweakConfig.Order.Length; i++) {
                order[i] = TweakConfig.Order[i];
            }
            if (!(order.Contains(0) && order.Contains(1) && order.Contains(2))) {
                order = new[] {0, 1, 2};
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
        };

        public override string Name => "Custom Time Formats";
        public override string Description => "Allows setting custom time formats for the in game clock. Uses C# formatting strings.";

        public unsafe delegate void SetText(AtkTextNode* self, byte* strPtr);
        private HookWrapper<SetText> setTextHook;
        private nint setTextAddress = nint.Zero;

        public override void Setup() {
            base.Setup();
            AddChangelog("1.8.8.0", "Fixed tooltip when hovering clocks");
            AddChangelog("1.8.8.0", "Returned 'click to change clock' feature from base game.");
        }

        protected override unsafe void Enable() {
            TweakConfig = LoadConfig<Config>() ?? PluginConfig.UiAdjustments.CustomTimeFormats ?? new Config(); 
            if (setTextAddress == nint.Zero) {
                setTextAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 41 BC ?? ?? ?? ?? 48 8D BD") + 9;
                SimpleLog.Verbose($"SetTextAddress: {setTextAddress.ToInt64():X}");
            }

            setTextHook ??= Common.Hook(setTextAddress, new SetText(SetTextDetour));
            setTextHook?.Enable();
            Common.FrameworkUpdate += OnFrameworkUpdate;
            base.Enable();
        }

        private unsafe void SetTextDetour(AtkTextNode* self, byte* strPtr) {
            if (self == textNodePtr) return; // Block update of Time String
            setTextHook.Original(self, strPtr);
        }

        protected override void Disable() {
            setTextHook?.Disable();
            SaveConfig(TweakConfig);
            PluginConfig.UiAdjustments.CustomTimeFormats = null;
            Common.FrameworkUpdate -= OnFrameworkUpdate;
            base.Disable();
        }

        public override void Dispose() {
            setTextHook?.Dispose();
            base.Dispose();
        }

        private unsafe AtkTextNode* textNodePtr = null;
        private unsafe void* textNodeVtablePtr = null;

        private string[] GetClockIcons() => Service.ClientState.ClientLanguage switch {
            ClientLanguage.German => new[] { $"{(char)SeIconChar.EorzeaTimeDe}", $"{(char)SeIconChar.LocalTimeDe}", $"{(char)SeIconChar.ServerTimeDe}" },
            ClientLanguage.French => new[] { $"{(char)SeIconChar.EorzeaTimeFr}", $"{(char)SeIconChar.LocalTimeFr}", $"{(char)SeIconChar.ServerTimeFr}" },
            _ => new[] { $"{(char)SeIconChar.EorzeaTimeEn}", $"{(char)SeIconChar.LocalTimeEn}", $"{(char)SeIconChar.ServerTimeEn}" },
        };

        private unsafe void UpdateTimeString(Utf8String xivString) {
            if (!(Service.GameConfig.TryGet(UiConfigOption.TimeEorzea, out bool showET) && 
                  Service.GameConfig.TryGet(UiConfigOption.TimeLocal, out bool showLT) && 
                  Service.GameConfig.TryGet(UiConfigOption.TimeServer, out bool showST))) return;
            
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
                Common.WriteSeString(xivString, timeSeString);
            }
        }

        private unsafe void OnFrameworkUpdate() {
            try {
                if (textNodePtr != null) {
                    if (textNodePtr->AtkResNode.VirtualTable == textNodeVtablePtr) {
                        UpdateTimeString(textNodePtr->NodeText);
                    } else {
                        SimpleLog.Verbose("Lost Text Node");
                        textNodePtr = null;
                    }

                    return;
                }

                var serverInfo = (AtkUnitBase*) Service.GameGui.GetAddonByName("_DTR", 1);
                if (serverInfo == null) return;
                textNodePtr = (AtkTextNode*) UiAdjustments.GetResNodeByPath(serverInfo->RootNode, Child, Previous, Child);
                if (textNodePtr == null) return;
                SimpleLog.Verbose($"Found Text Node: {(ulong) textNodePtr:X}");
                textNodeVtablePtr = textNodePtr->AtkResNode.VirtualTable;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            
        }
    }
}
