using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace SimpleTweaksPlugin.Utility;

public static class HotkeyHelper {
    private static string? _settingKey;
    private static string? _focused;
    private static readonly List<VirtualKey> NewKeys = [];
    private static readonly Stopwatch Safety = Stopwatch.StartNew();
    
    private static void CheckSafety() {
        if (Safety.IsRunning && Safety.ElapsedMilliseconds > 500) {
            SimpleLog.Verbose("Hotkey editor safety triggered.");
            _settingKey = null;
            _focused = null;
            Safety.Reset();
        } 
    }
    
    public static bool CheckHotkeyState(VirtualKey[] keys, bool clearOnPressed = true) {
        CheckSafety();
        if (!string.IsNullOrEmpty(_settingKey)) return false; // Ignore hotkeys if one is being assigned.
        foreach (var vk in Service.KeyState.GetValidVirtualKeys()) {
            if (keys.Contains(vk)) {
                if (!Service.KeyState[vk]) return false;
            } else {
                if (Service.KeyState[vk]) return false;
            }
        }

        if (clearOnPressed) {
            foreach (var k in keys) {
                Service.KeyState[(int)k] = false;
            }
        }
        
        return true;
    }

    public static bool DrawHotkeyConfigEditor(string name, VirtualKey[] keys, [NotNullWhen(true)] out VirtualKey[] outKeys) {
        outKeys = [];
        var modified = false;
        var identifier = name.Contains("###") ? $"{name.Split("###", 2)[1]}" : name;
        var strKeybind = string.Join("+", keys.Select(k => k.GetKeyName()));

        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);

        if (_settingKey == identifier) {
            if (ImGui.GetIO()
                    .KeyAlt && !NewKeys.Contains(VirtualKey.MENU))
                NewKeys.Add(VirtualKey.MENU);
            if (ImGui.GetIO()
                    .KeyShift && !NewKeys.Contains(VirtualKey.SHIFT))
                NewKeys.Add(VirtualKey.SHIFT);
            if (ImGui.GetIO()
                    .KeyCtrl && !NewKeys.Contains(VirtualKey.CONTROL))
                NewKeys.Add(VirtualKey.CONTROL);

            for (var k = 0;
                 k < ImGui.GetIO()
                     .KeysDown.Length && k < 160;
                 k++) {
                if (ImGui.GetIO()
                    .KeysDown[k]) {
                    if (!NewKeys.Contains((VirtualKey)k)) {
                        if ((VirtualKey)k == VirtualKey.ESCAPE) {
                            _settingKey = null;
                            NewKeys.Clear();
                            _focused = null;
                            break;
                        }

                        NewKeys.Add((VirtualKey)k);
                    }
                }
            }

            NewKeys.Sort();
            strKeybind = string.Join("+", NewKeys.Select(k => k.GetKeyName()));
        }

        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2))
        using (ImRaii.PushColor(ImGuiCol.Border, 0xFF00A5FF, _settingKey == identifier)) {
            ImGui.InputText(name, ref strKeybind, 100, ImGuiInputTextFlags.ReadOnly);
        }

        var active = ImGui.IsItemActive();

        if (_settingKey == identifier) {
            if (_focused != identifier) {
                ImGui.SetKeyboardFocusHere(-1);
                _focused = identifier;
            } else {
                Safety.Restart();
                ImGui.SameLine();
                
                if (ImGui.Button(NewKeys.Count > 0 ? Loc.Localize("HotkeyHelper.Confirm", "Confirm") + $"##{identifier}" : Loc.Localize("HotkeyHelper.Cancel", "Cancel") + $"##{identifier}")) {
                    Safety.Reset();
                    _settingKey = null;
                    if (NewKeys.Count > 0) {
                        outKeys = NewKeys.ToArray();
                        modified = true;
                    }

                    NewKeys.Clear();
                } else {
                    if (!active) {
                        Safety.Reset();
                        _focused = null;
                        _settingKey = null;
                        if (NewKeys.Count > 0) {
                            outKeys = NewKeys.ToArray();
                            modified = true;
                        }

                        NewKeys.Clear();
                    }
                }
            }
        } else {
            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("HotkeyHelper.Set", "Set Keybind") + $"###setHotkeyButton{identifier}")) {
                Safety.Restart();
                _settingKey = identifier;
            }
        }

        return modified;
    }
}
