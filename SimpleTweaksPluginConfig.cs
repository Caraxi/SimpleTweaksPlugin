using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private SimpleTweaksPlugin plugin;

        public int Version { get; set; } = 2;

        public SimpleTweaksPluginConfig() { }

        public List<string> EnabledTweaks = new List<string>();

        public bool HideKofi;
        public bool ShowExperimentalTweaks;

        public void Init(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }

        public bool DrawConfigUI() {
            var drawConfig = true;
            var scale = ImGui.GetIO().FontGlobalScale;
            var windowFlags = ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale), new Vector2(600 * scale, 800 * scale));
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);

            if (!HideKofi) {
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF5E5BFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF5E5BAA);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5E5BDD);
                var c = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize("Support on Ko-fi").X);
                if (ImGui.SmallButton("Support on Ko-fi")) {
                    Process.Start("https://ko-fi.com/Caraxi");
                }
                ImGui.SetCursorPos(c);
                ImGui.PopStyleColor(3);
            }

            ImGui.Text("Enable or disable any tweaks here.\nAll tweaks are disabled by default.");
            
            ImGui.Separator();

            foreach (var t in plugin.Tweaks) {
                var enabled = t.Enabled;
                if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                    if (enabled) {
#if DEBUG
                        PluginLog.Log($"Enable: {t.Name}");
#endif
                        t.Enable();
                        if (t.Enabled) {
                            EnabledTweaks.Add(t.GetType().Name);
                        }
                    } else {
#if DEBUG
                        PluginLog.Log($"Disable: {t.Name}");
#endif
                        t.Disable();
                        EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                    }
                    Save();
                }
                ImGui.SameLine();

                var changed = false;
                t.DrawConfig(ref changed);
                if (changed) Save();

                ImGui.Separator();

               
            }
            
            var a = false;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x0);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0x0);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0x0);
            ImGui.Checkbox("###notARealCheckbox", ref a);
            ImGui.PopStyleColor(3);
            ImGui.SameLine();
            if (ImGui.TreeNode("General Options")) {
                if (ImGui.Checkbox("Show Experimental Tweaks.", ref ShowExperimentalTweaks)) Save();
                if (ImGui.Checkbox("Hide Ko-fi link.", ref HideKofi)) Save();
                ImGui.TreePop();
            }

            ImGui.End();

            return drawConfig;
        }
    }
}