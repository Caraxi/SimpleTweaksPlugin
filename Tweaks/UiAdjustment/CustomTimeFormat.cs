using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
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

        }

        private float maxX = 0;
        private bool overSized = false;
        private int maxTextSize = 0;

        public override unsafe void DrawConfig(ref bool hasChanged) {
            if (Enabled) {
                if (ImGui.TreeNode(Name)) {
                    if (Experimental) {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
                    }

                    var icons = GetClockIcons();

                    var et = DateTimeOffset.FromUnixTimeSeconds(*(long*)(PluginInterface.Framework.Address.BaseAddress + 0x1608));
                    var lt = DateTimeOffset.Now;

                    ImGui.Text(icons[0]);
                    ImGui.SameLine();
                    hasChanged |= ImGui.Checkbox("###enableET", ref PluginConfig.UiAdjustments.CustomTimeFormats.ShowET);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(120);
                    hasChanged |= ImGui.InputText("Eorzea Time###editFormatET", ref PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatET, 50);
                    
                    ImGui.SameLine();
                    if (ImGui.GetCursorPosX() > maxX) maxX = ImGui.GetCursorPosX();
                    ImGui.SetCursorPosX(maxX);
                    try {
                        var preview = $"{et.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatET)}";
                        ImGui.SetNextItemWidth(120);
                        ImGui.InputText("###previewET", ref preview, 50, ImGuiInputTextFlags.ReadOnly);
                    } catch {
                        ImGui.Text("Format Invalid");
                    }
                    
                    ImGui.Text(icons[1]);
                    ImGui.SameLine();
                    hasChanged |= ImGui.Checkbox("###enableLT", ref PluginConfig.UiAdjustments.CustomTimeFormats.ShowLT);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(120);
                    hasChanged |= ImGui.InputText("Local Time###editFormatLT", ref PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatLT, 50);
                    
                    ImGui.SameLine();
                    if (ImGui.GetCursorPosX() > maxX) maxX = ImGui.GetCursorPosX();
                    ImGui.SetCursorPosX(maxX);
                    try {
                        var preview = $"{lt.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatLT)}";
                        ImGui.SetNextItemWidth(120);
                        ImGui.InputText("###previewLT", ref preview, 50, ImGuiInputTextFlags.ReadOnly);
                    } catch {
                        ImGui.Text("Format Invalid");
                    }
                    
                    ImGui.Text(icons[2]);
                    ImGui.SameLine();
                    hasChanged |= ImGui.Checkbox("###enableST", ref PluginConfig.UiAdjustments.CustomTimeFormats.ShowST);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(120);
                    hasChanged |= ImGui.InputText("Server Time (UTC)###editFormatST", ref PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatST, 50);
                    ImGui.SameLine();
                    if (ImGui.GetCursorPosX() > maxX) maxX = ImGui.GetCursorPosX();
                    ImGui.SetCursorPosX(maxX);
                    try {
                        var preview = $"{lt.UtcDateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatST)}";
                        ImGui.SetNextItemWidth(120);
                        ImGui.InputText("###previewST", ref preview, 50, ImGuiInputTextFlags.ReadOnly);
                    } catch {
                        ImGui.Text("Format Invalid");
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

        private unsafe AtkTextNode* textNodePtr = null;
        private unsafe void* textNodeVtablePtr = null;

        private string[] GetClockIcons() => PluginInterface.ClientState.ClientLanguage switch {
            ClientLanguage.German => new[] { $"{(char)SeIconChar.EorzeaTimeDe}", $"{(char)SeIconChar.LocalTimeDe}", $"{(char)SeIconChar.ServerTimeDe}" },
            ClientLanguage.French => new[] { $"{(char)SeIconChar.EorzeaTimeFr}", $"{(char)SeIconChar.LocalTimeFr}", $"{(char)SeIconChar.ServerTimeFr}" },
            ClientLanguage.Japanese => new[] { $"{(char)SeIconChar.EorzeaTimeJa}", $"{(char)SeIconChar.LocalTimeJa}", $"{(char)SeIconChar.ServerTimeJa}" },
            _ => new[] { $"{(char)SeIconChar.EorzeaTimeEn}", $"{(char)SeIconChar.LocalTimeEn}", $"{(char)SeIconChar.ServerTimeEn}" },
        };


        private unsafe void UpdateTimeString(FFXIVString xivString) {
            var icons = GetClockIcons();
            var et = DateTimeOffset.FromUnixTimeSeconds(*(long*)(PluginInterface.Framework.Address.BaseAddress + 0x1608));
            var lt = DateTimeOffset.Now;
            var timeSeString = new SeString(new List<Payload>());
            
            try {
                if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowET)
                    timeSeString.Payloads.Add(new TextPayload($"{icons[0]} {et.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatET)} "));
                if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowLT)
                    timeSeString.Payloads.Add(new TextPayload($"{icons[1]} {lt.DateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatLT)} "));
                if (PluginConfig.UiAdjustments.CustomTimeFormats.ShowST)
                    timeSeString.Payloads.Add(new TextPayload($"{icons[2]} {lt.UtcDateTime.ToString(PluginConfig.UiAdjustments.CustomTimeFormats.CustomFormatST)} "));
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
