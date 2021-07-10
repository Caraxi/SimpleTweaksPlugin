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

            if (PluginConfig.ShowTweakIDs) {
                ImGui.SameLine();
                var minPos = ImGui.GetCursorPosX();
                var text = $"[{this.Key}]";
                var size = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosX(Math.Max(minPos, ImGui.GetWindowContentRegionWidth() - size.X));
                ImGui.TextDisabled(text);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
                if (ImGui.IsItemClicked()) {
                    ImGui.SetClipboardText(Key);
                }
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
                #if DEBUG
                SimpleLog.Log($"Save Config: {Name}");
                #endif
                var configDirectory = PluginInterface.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, this.Key + ".json");
                var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);
                #if DEBUG
                foreach (var l in jsonString.Split('\n')) {
                    SimpleLog.Log($"    [{Name} Config] {l}");
                }
                #endif
                File.WriteAllText(configFile, jsonString);
            } catch (Exception ex) {
                SimpleLog.Error($"Failed to write config for tweak: {this.Name}");
                SimpleLog.Error(ex);
            }
        }

        public virtual void RequestSaveConfig() {
            try {
                #if DEBUG
                SimpleLog.Log($"Request Save Config: {Name}");
                #endif
                var configObj = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig)))?.GetValue(this);
                if (configObj == null) return;
                SaveConfig((TweakConfig) configObj);
            } catch (Exception ex) {
                SimpleLog.Error($"Failed to save config for tweak: {this.Name}");
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

                var configOptionIndex = 0;
                foreach (var (f, attr) in fields) {
                    if (f.FieldType == typeof(bool)) {
                        var v = (bool) f.GetValue(configObj);
                        if (ImGui.Checkbox($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v)) {
                            f.SetValue(configObj, v);
                        }
                    } else if (f.FieldType == typeof(int)) {
                        var v = (int) f.GetValue(configObj);
                        ImGui.SetNextItemWidth(attr.EditorSize == -1 ? -1 : attr.EditorSize * ImGui.GetIO().FontGlobalScale);
                        var e = attr.IntType switch {
                            TweakConfigOptionAttribute.IntEditType.Slider => ImGui.SliderInt($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, attr.IntMin, attr.IntMax),
                            _ => false
                        };
                        
                        if (v < attr.IntMin) {
                            v = attr.IntMin;
                            e = true;
                        }

                        if (v > attr.IntMax) {
                            v = attr.IntMax;
                            e = true;
                        }
                        
                        if (e) {
                            f.SetValue(configObj, v);
                        }
                    }
                    else {
                        ImGui.Text($"Invalid Auto Field Type: {f.Name}");
                    }

                }

            } catch (Exception ex) {
                ImGui.Text($"Error with AutoConfig: {ex.Message}");
                ImGui.TextWrapped($"{ex.StackTrace}");
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
