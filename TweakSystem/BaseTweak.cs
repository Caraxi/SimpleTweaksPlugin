using System.Numerics;
using System;
using System.IO;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;

namespace SimpleTweaksPlugin.TweakSystem {
    public abstract class BaseTweak {
        protected SimpleTweaksPlugin Plugin;
        protected DalamudPluginInterface PluginInterface;
        protected SimpleTweaksPluginConfig PluginConfig;

        public virtual bool Ready { get; protected set; }
        public virtual bool Enabled { get; protected set; }

        public virtual string Key => GetType().Name;

        public abstract string Name { get; }
        public virtual string Description => null;
        protected virtual string Author => null;
        public virtual bool Experimental => false;

        public virtual bool CanLoad => true;

        public void InterfaceSetup(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = config;
            this.Plugin = plugin;
        }

        private void DrawCommon() {
            if (this.Experimental) {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
            }

            if (!string.IsNullOrEmpty(Author)) {
                ImGui.SameLine();
                ImGui.TextDisabled($"  by {Author}");
            }
        }

        protected T LoadConfig<T>() where T : TweakConfig {
            try {
                var configDirectory = PluginInterface.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, this.Key + ".json");
                if (!File.Exists(configFile)) return default;
                var jsonString = File.ReadAllText(configFile);
                return JsonConvert.DeserializeObject<T>(jsonString);
            } catch (Exception ex) {
                SimpleLog.Error($"Failed to load config for tweak: {Name}");
                SimpleLog.Error(ex);
                return default;
            }
        }

        protected void SaveConfig<T>(T config) where T : TweakConfig {
            try {
                var configDirectory = PluginInterface.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, this.Key + ".json");
                var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFile, jsonString);
            } catch (Exception ex) {
                SimpleLog.Error($"Failed to write config for tweak: {this.Name}");
                SimpleLog.Error(ex);
            }
        }
        
        public bool DrawConfig(ref bool hasChanged) {
            var configTreeOpen = false;
            if (DrawConfigTree != null && Enabled) {
                var x = ImGui.GetCursorPosX();
                if (ImGui.TreeNode($"{Name}##treeConfig_{GetType().Name}")) {
                    configTreeOpen = true;
                    DrawCommon();
                    ImGui.SetCursorPosX(x);
                    ImGui.BeginGroup();
                    DrawConfigTree(ref hasChanged);
                    ImGui.EndGroup();
                    ImGui.TreePop();
                } else {
                    DrawCommon();
                }
            } else {
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                ImGui.TreeNodeEx(Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                DrawCommon();
            }

            return configTreeOpen;
        }

        protected delegate void DrawConfigDelegate(ref bool hasChanged);
        protected virtual DrawConfigDelegate DrawConfigTree => null;
        
        public virtual void Setup() {
            Ready = true;
        }

        public virtual void Enable() {
            Enabled = true;
        }

        public virtual void Disable() {
            Enabled = false;
        }

        public virtual void Dispose() {
            Ready = false;
        }


    }
}
