using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
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

            var windowFlags = ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 200), new Vector2(600, 800));
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);

            foreach (var t in plugin.Tweaks) {

                var enabled = t.Enabled;

                if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
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
                ImGui.SameLine();

                if (t.DrawConfig()) {
                    Save();
                }

                ImGui.Separator();

               
            }
            
            ImGui.End();

            return drawConfig;
        }
    }
}