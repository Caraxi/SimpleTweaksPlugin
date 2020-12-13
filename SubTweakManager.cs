using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace SimpleTweaksPlugin {

    public abstract class SubTweakManager : Tweak {

    } 

    public abstract class SubTweakManager<T> : SubTweakManager where T : BaseTweak {

        public List<T> SubTweaks = new List<T>();

        public string GetTweakKey(T t) {
            return $"{GetType().Name}@{t.GetType().Name}";
        }

        public override void DrawConfig(ref bool change) {

            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {

                foreach (var t in SubTweaks) {
                    var key = GetTweakKey(t);
                    var subTweakEnabled = Enabled && t.Enabled;
                    if (!Enabled) {
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
                    }
                    if (ImGui.Checkbox($"###{GetTweakKey(t)}enabledCheckbox", ref subTweakEnabled)) {
                        if (Enabled) {
                            if (subTweakEnabled) {
                                t.Enable();
                                if (!this.PluginConfig.EnabledTweaks.Contains(key)) this.PluginConfig.EnabledTweaks.Add(key);
                                change = true;
                            } else {
                                t.Disable();
                                if (this.PluginConfig.EnabledTweaks.Contains(key)) this.PluginConfig.EnabledTweaks.Remove(key);
                                change = true;
                            }
                        }
                    }
                    if (!Enabled) ImGui.PopStyleColor(3);
                    ImGui.SameLine();
                    t.DrawConfig(ref change);
                }

                ImGui.TreePop();
            }
        }

        public override void Setup() {

            var tweakList = new List<T>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(T)))) {
                var tweak = (T) Activator.CreateInstance(t);
                tweak.InterfaceSetup(this.Plugin, this.PluginInterface, this.PluginConfig);
                tweak.Setup();
                tweakList.Add(tweak);
            }

            SubTweaks = tweakList.OrderBy(t => t.Name).ToList();

            Ready = true;
        }
        
        public override void Enable() {
            foreach (var t in SubTweaks) {
                if (PluginConfig.EnabledTweaks.Contains(GetTweakKey(t))) {
                    t.Enable();
                }
            }

            Enabled = true;
        }

        public override void Disable() {
            foreach (var t in SubTweaks) {
                t.Disable();
            }

            Enabled = false;
        }

        public override void Dispose() {
            foreach (var t in SubTweaks) {
                t.Disable();
                t.Dispose();
            }

            Ready = false;
        }
    }
}
