using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace SimpleTweaksPlugin {
    public class SimpleTweaksPluginConfig : IPluginConfiguration {
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private SimpleTweaksPlugin plugin;

        public int Version { get; set; }

        public SimpleTweaksPluginConfig() { }

        public List<string> EnabledTweaks = new List<string>();


        public void Init(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }

        public bool DrawConfigUI() {
            var drawConfig = true;
            var windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);

            foreach (var t in plugin.Tweaks) {

                var enabled = t.Enabled;

                if (ImGui.Checkbox(t.Name, ref enabled)) {
                    if (enabled) {
                        t.Enable();
                        if (t.Enabled) {
                            EnabledTweaks.Add(t.GetType().Name);
                        }
                    } else {
                        t.Disable();
                        EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                    }
                    Save();
                }

            }
            
            ImGui.End();

            return drawConfig;
        }
    }
}