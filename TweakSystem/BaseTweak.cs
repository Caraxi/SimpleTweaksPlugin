using System.Numerics;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        public string LocalizedName => LocString("Name", Name, "Tweak Name");

        public virtual string Description => null;
        protected virtual string Author => null;
        public virtual bool Experimental => false;
        public virtual IEnumerable<string> Tags { get; } = new string[0];

        public virtual bool CanLoad => true;

        public virtual bool UseAutoConfig => false;

        protected CultureInfo Culture => Plugin.Culture;

        public void InterfaceSetup(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = config;
            this.Plugin = plugin;
        }

        public string LocString(string key, string fallback, string description = null) {
            description ??= $"{Name} - {fallback}";
            return Loc.Localize($"{this.Key} / {key}", fallback, $"[{this.GetType().Name}] {description}");
        }

        public string LocString(string keyAndFallback) {
            return LocString(keyAndFallback, keyAndFallback);
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
                SimpleLog.Verbose($"Save Config: {Name}");
                #endif
                var configDirectory = PluginInterface.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, this.Key + ".json");
                var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);
                #if DEBUG
                foreach (var l in jsonString.Split('\n')) {
                    SimpleLog.Verbose($"    [{Name} Config] {l}");
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
                if (ImGui.TreeNode($"{LocalizedName}##treeConfig_{GetType().Name}")) {
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
                ImGui.TreeNodeEx(LocalizedName, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                DrawCommon();
            }

            if (hasChanged) ConfigChanged();
            return configTreeOpen;
        }

        protected virtual void ConfigChanged() { }

        public virtual void LanguageChanged() { }

        private void DrawAutoConfig() {
            var configChanged = false;
            try {
                // ReSharper disable once PossibleNullReferenceException
                var configObj = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig))).GetValue(this);


                var fields = configObj.GetType().GetFields()
                    .Where(f => f.GetCustomAttribute(typeof(TweakConfigOptionAttribute)) != null)
                    .Select(f => (f, (TweakConfigOptionAttribute) f.GetCustomAttribute(typeof(TweakConfigOptionAttribute))))
                    .OrderBy(a => a.Item2.Priority).ThenBy(a => a.Item2.Name);

                var configOptionIndex = 0;
                foreach (var (f, attr) in fields) {
                    var localizedName = LocString(attr.LocalizeKey, attr.Name, $"[Config] {attr.Name}");
                    if (attr.Editor != null) {
                        var v = f.GetValue(configObj);
                        var arr = new [] {$"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", v};
                        var o = (bool) attr.Editor.Invoke(null, arr);
                        if (o) {
                            configChanged = true;
                            f.SetValue(configObj, arr[1]);
                        }
                    } else if (f.FieldType == typeof(bool)) {
                        var v = (bool) f.GetValue(configObj);
                        if (ImGui.Checkbox($"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v)) {
                            configChanged = true;
                            f.SetValue(configObj, v);
                        }
                    } else if (f.FieldType == typeof(int)) {
                        var v = (int) f.GetValue(configObj);
                        ImGui.SetNextItemWidth(attr.EditorSize == -1 ? -1 : attr.EditorSize * ImGui.GetIO().FontGlobalScale);
                        var e = attr.IntType switch {
                            TweakConfigOptionAttribute.IntEditType.Slider => ImGui.SliderInt($"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, attr.IntMin, attr.IntMax),
                            TweakConfigOptionAttribute.IntEditType.Drag => ImGui.DragInt($"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, 1f, attr.IntMin, attr.IntMax),
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
                            configChanged = true;
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

            if (configChanged) {
                ConfigChanged();
            }
        }

        public virtual void HandleBasicCommand(string[] args) {
            SimpleLog.Debug($"[{Key}] Command Handler: {string.Join(" , ", args)}");
            if (UseAutoConfig) {
                if (!Enabled) {
                    Service.Chat.PrintError($"'{Name}' is not enabled.");
                    return;
                }
                var configObj = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig)))?.GetValue(this);
                if (configObj != null) {
                    var fields = configObj.GetType().GetFields()
                        .Select(f => (f, (TweakConfigOptionAttribute) f.GetCustomAttribute(typeof(TweakConfigOptionAttribute))))
                        .OrderBy(a => a.Item2.Priority).ThenBy(a => a.Item2.Name);
                    
                    if (args.Length > 1) {
                        var field = fields.FirstOrDefault(f => f.f.Name == args[0]);
                        if (field != default) {
                            SimpleLog.Debug($"Set Value of {field.f.Name}");
                        
                            if (field.f.FieldType == typeof(bool)) {

                                switch (args[1]) {
                                    case "1":
                                    case "enable":
                                    case "e":
                                    case "on": {
                                        field.f.SetValue(configObj, true);
                                        break;
                                    }
                                    case "o":
                                    case "disable":
                                    case "d":
                                    case "off": {
                                        field.f.SetValue(configObj, false);
                                        break;
                                    }
                                    case "t":
                                    case "toggle": {
                                        var v = (bool) field.f.GetValue(configObj);
                                        field.f.SetValue(configObj, !v);
                                        break;
                                    }
                                    default: {
                                        Service.Chat.PrintError($"'{args[1]}' is not a valid value for a boolean.");
                                        return;
                                    }
                                }
                                
                                RequestSaveConfig();
                            } else if (field.f.FieldType == typeof(int)) {
                                var isValidInt = int.TryParse(args[1], out var val);
                                if (isValidInt && val >= field.Item2.IntMin && val <= field.Item2.IntMax) {
                                    field.f.SetValue(configObj, val);
                                    RequestSaveConfig();
                                } else {
                                    Service.Chat.PrintError($"'{args[1]}' is not a valid integer between {field.Item2.IntMin} and {field.Item2.IntMax}.");
                                }
                            }
                            
                            return;
                        }
                    }

                    // Print all options
                    if (args.Length == 0) Service.Chat.PrintError($"'{Name}' Command Config:");
                    foreach (var aField in fields) {
                        if (args.Length > 0) {
                            if (args[0] != aField.f.Name) continue;
                        }
                        var valuesString = string.Empty;
                        if (aField.f.FieldType == typeof(bool)) {
                            valuesString = $"on|off";
                        } else if (aField.f.FieldType == typeof(int)) {
                            valuesString = $"{aField.Item2.IntMin} - {aField.Item2.IntMax}";
                        }

                        if (!string.IsNullOrEmpty(valuesString)) {
                            var line = $"/tweaks {Key} {aField.f.Name} [{valuesString}]";
                            Service.Chat.PrintError($"   - {line}");
                        }
                    }
                    
                    return;

                } else {
                    SimpleLog.Debug($"{Key} has no Config Object");
                }
            }
            
            Service.Chat.PrintError($"'{Name}' does not support command usage.");
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
