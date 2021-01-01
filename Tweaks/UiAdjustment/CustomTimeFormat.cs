using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Interface;
using FFXIVClientStructs;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public CustomTimeFormat.Config CustomTimeFormats = new CustomTimeFormat.Config();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class CustomTimeFormat : UiAdjustments.SubTweak {
        public class Config {

            public bool ShowET = true;
            public bool ShowLT = true;
            public bool ShowST = true;
            
            public string CustomFormatET = "HH:mm:ss";
            public string CustomFormatLT = "HH:mm:ss";
            public string CustomFormatST = "HH:mm:ss";

            public int[] Order = { 0, 1, 2 };
        }

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
                if (hoveringUp && ImGui.IsMouseClicked(0)) {
                    moveAction = new MoveAction() {Index = index, MoveUp = true};
                }
            }

            ImGui.SetCursorPos(p3);
            if (index < 2) {
                ImGui.TextColored(hoveringDown ? other : white, down);
                if (hoveringDown && ImGui.IsMouseClicked(0)) {
                    moveAction = new MoveAction() { Index = index, MoveUp = false };
                }
            }

            ImGui.EndGroup();
            ImGui.SetCursorPos(p2 + new Vector2(s.X, 0));
            ImGui.PopFont();
            ImGui.SetWindowFontScale(1.0f);
            // End Reordering

            hasChanged |= ImGui.Checkbox("###enableET", ref enabled);
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
            var c = PluginConfig.UiAdjustments.CustomTimeFormats;
            switch (id) {
                case 0: {
                    var et = DateTimeOffset.FromUnixTimeSeconds(*(long*)(PluginInterface.Framework.Address.BaseAddress + 0x1608));
                    DrawClockConfig(index, "Eorzea Time", icons[0], ref hasChanged, ref c.ShowET, ref c.CustomFormatET, ref moveAction, et);
                    break;
                }
                case 1: {
                    DrawClockConfig(index, "Local Time", icons[1], ref hasChanged, ref c.ShowLT, ref c.CustomFormatLT, ref moveAction, DateTimeOffset.Now);
                    break;
                }
                case 2: {
                    DrawClockConfig(index, "Server Time", icons[2], ref hasChanged, ref c.ShowST, ref c.CustomFormatST, ref moveAction, DateTimeOffset.Now.UtcDateTime);
                    break;
                }
                default: {
                    // Broken
                    c.Order = new[] {0, 1, 2};
                    SimpleLog.Error("Broken Config Detected. Automatically Fixed");
                    hasChanged = true;
                    return false;
                }
            }

            return true;
        }

        public override void DrawConfig(ref bool hasChanged) {
            if (Enabled) {
                if (ImGui.TreeNode(Name)) {
                    if (Experimental) {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
                    }

                    var icons = GetClockIcons();
                    
                    var c = PluginConfig.UiAdjustments.CustomTimeFormats;

                    // Safety
                    var order = new[] { -1, -1, -1};
                    if (c.Order.Length != 3) {
                        c.Order = new[] { 0, 1, 2};
                        SimpleLog.Error("Broken Config Detected. Automatically Fixed");
                        hasChanged = true;
                    }
                    for (var i = 0; i < c.Order.Length; i++) {
                        order[i] = c.Order[i];
                    }
                    if (!(order.Contains(0) && order.Contains(1) && order.Contains(2))) {
                        order = new[] {0, 1, 2};
                        c.Order = new[] { 0, 1, 2 };
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
                                var moving = c.Order[moveAction.Index];
                                var replacing = c.Order[moveAction.Index - 1];
                                c.Order[moveAction.Index - 1] = moving;
                                c.Order[moveAction.Index] = replacing;
                                hasChanged = true;
                            }
                        } else {
                            if (moveAction.Index < 2) {
                                var moving = c.Order[moveAction.Index];
                                var replacing = c.Order[moveAction.Index + 1];
                                c.Order[moveAction.Index + 1] = moving;
                                c.Order[moveAction.Index] = replacing;
                                hasChanged = true;
                            }
                        }
                    }

                    ImGui.TreePop();
                } else if (Experimental) {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
                }
            } else {
                base.DrawConfig(ref hasChanged);
            }
        }

        public override string Name => "Custom Time Formats";

        public unsafe delegate void SetText(AtkTextNode* self, byte* strPtr);
        private Hook<SetText> setTextHook;
        private IntPtr setTextAddress = IntPtr.Zero;

        public override unsafe void Enable() {
            if (setTextAddress == IntPtr.Zero) {
                setTextAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 49 8B FC") + 9;
                SimpleLog.Verbose($"SetTextAddress: {setTextAddress.ToInt64():X}");
            }

            setTextHook ??= new Hook<SetText>(setTextAddress, new SetText(SetTextDetour));
            setTextHook?.Enable();
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            base.Enable();
        }

        private unsafe void SetTextDetour(AtkTextNode* self, byte* strPtr) {
            if (self == textNodePtr) return; // Block update of Time String
            setTextHook.Original(self, strPtr);
        }

        public override void Disable() {
            setTextHook?.Disable();
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            base.Disable();
        }

        public override void Dispose() {
            setTextHook?.Dispose();
            base.Dispose();
        }

        private unsafe AtkTextNode* textNodePtr = null;
        private unsafe void* textNodeVtablePtr = null;

        private string[] GetClockIcons() => PluginInterface.ClientState.ClientLanguage switch {
            ClientLanguage.German => new[] { $"{(char)SeIconChar.EorzeaTimeDe}", $"{(char)SeIconChar.LocalTimeDe}", $"{(char)SeIconChar.ServerTimeDe}" },
            ClientLanguage.French => new[] { $"{(char)SeIconChar.EorzeaTimeFr}", $"{(char)SeIconChar.LocalTimeFr}", $"{(char)SeIconChar.ServerTimeFr}" },
            _ => new[] { $"{(char)SeIconChar.EorzeaTimeEn}", $"{(char)SeIconChar.LocalTimeEn}", $"{(char)SeIconChar.ServerTimeEn}" },
        };

        private unsafe void UpdateTimeString(FFXIVString xivString) {
            var icons = GetClockIcons();
            var et = DateTimeOffset.FromUnixTimeSeconds(*(long*)(PluginInterface.Framework.Address.BaseAddress + 0x1608));
            var lt = DateTimeOffset.Now;
            var timeSeString = new SeString(new List<Payload>());

            try {
                foreach (var c in PluginConfig.UiAdjustments.CustomTimeFormats.Order) {
                    switch (c) {
                        case 0: {
                            if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowET)
                                timeSeString.Payloads.Add(new TextPayload($"{icons[0]} {et.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatET)} "));
                            break;
                        }
                        case 1: {
                            if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowLT)
                                timeSeString.Payloads.Add(new TextPayload($"{icons[1]} {lt.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatLT)} "));
                            break;
                        }
                        case 2: {
                            if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowST)
                                timeSeString.Payloads.Add(new TextPayload($"{icons[2]} {lt.UtcDateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatST)} "));
                            break;
                        }
                    }
                }
            } catch {
                timeSeString.Payloads.Add(new TextPayload("Invalid Time Format"));
            }

            if (timeSeString.Payloads.Count > 0) {
                Plugin.Common.WriteSeString(xivString, timeSeString);
            }
        }

        private unsafe void OnFrameworkUpdate(Framework framework) {
            if (textNodePtr != null) {
                if (textNodePtr->AtkResNode.AtkEventTarget.vtbl == textNodeVtablePtr) {
                    UpdateTimeString(textNodePtr->NodeText);
                } else {
                    SimpleLog.Verbose("Lost Text Node");
                    textNodePtr = null;
                }
                return;
            }
            var serverInfo = (AtkUnitBase*) framework.Gui.GetUiObjectByName("_DTR", 1);
            if (serverInfo == null) return;
            textNodePtr = (AtkTextNode*) UiAdjustments.GetResNodeByPath(serverInfo->RootNode, Child, Previous, Child);
            if (textNodePtr == null) return;
            SimpleLog.Verbose($"Found Text Node: {(ulong)textNodePtr:X}");
            textNodeVtablePtr = textNodePtr->AtkResNode.AtkEventTarget.vtbl;
        }
    }
}
