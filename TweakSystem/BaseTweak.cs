using System.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public virtual IEnumerable<string> Tags { get; } = new string[0];

        public virtual bool CanLoad => true;

        public virtual bool UseAutoConfig => false;

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
            if ((UseAutoConfig || DrawConfigTree != null) && Enabled) {
                var x = ImGui.GetCursorPosX();
                if (ImGui.TreeNode($"{Name}##treeConfig_{GetType().Name}")) {
                    configTreeOpen = true;
                    DrawCommon();
                    ImGui.SetCursorPosX(x);
                    ImGui.BeginGroup();
                    if (UseAutoConfig)
                        DrawAutoConfig();
                    else 
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

        private void DrawAutoConfig() {

            try {
                // ReSharper disable once PossibleNullReferenceException
                var configObj = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig))).GetValue(this);


                var fields = configObj.GetType().GetFields()
                    .Select(f => (f, (TweakConfigOptionAttribute) f.GetCustomAttribute(typeof(TweakConfigOptionAttribute))))
                    .OrderBy(a => a.Item2.Priority).ThenBy(a => a.Item2.Name);

                foreach (var (f, attr) in fields) {
                    if (f.FieldType == typeof(bool)) {
                        var v = (bool) f.GetValue(configObj);
                        if (ImGui.Checkbox($"{attr.Name}##{f.Name}_{this.GetType().Name}", ref v)) {
                            f.SetValue(configObj, v);
                        }
                    } else {
                        ImGui.Text($"Invalid Auto Field Type: {f.Name}");
                    }

                }

            } catch {
                ImGui.Text("Error with AutoConfig");
            }
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
