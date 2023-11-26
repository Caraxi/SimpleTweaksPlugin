﻿using System.Numerics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.TweakSystem; 

public abstract class BaseTweak {
    protected SimpleTweaksPlugin Plugin;
    protected DalamudPluginInterface PluginInterface;
    protected SimpleTweaksPluginConfig PluginConfig;

    public virtual bool Ready { get; protected set; }
    public virtual bool Enabled { get; protected set; }
    protected virtual bool Unloading { get; private set; } = true;

    private bool hasPreviewImage;
    private IDalamudTextureWrap previewImage;
    
    public bool IsDisposed { get; private set; }

    public virtual string Key => GetType().Name;

    public virtual string Name => TweakNameAttribute?.Name ?? GetType().Name;

    public virtual uint Version => TweakVersionAttribute?.Version ?? 1;

    public string LocalizedName => LocString("Name", Name, "Tweak Name");

    public virtual string Description => TweakDescriptionAttribute?.Description;
    protected virtual string Author => TweakAuthorAttribute?.Author;
    public virtual bool Experimental => false;
    public virtual IEnumerable<string> Tags { get; } = new string[0];
    internal bool ForceOpenConfig { private get; set; }

    public TweakProvider TweakProvider { get; private set; } = null;
    public SubTweakManager TweakManager { get; private set; } = null;

    public virtual bool CanLoad => true;

    public virtual bool UseAutoConfig => TweakAutoConfigAttribute is not NoAutoConfig;

    protected CultureInfo Culture => Plugin.Culture;

    public void InterfaceSetup(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config, TweakProvider tweakProvider, SubTweakManager tweakManager = null) {
        this.PluginInterface = pluginInterface;
        this.PluginConfig = config;
        this.Plugin = plugin;
        this.TweakProvider = tweakProvider;
        this.TweakManager = tweakManager;
        
    }

    public string LocString(string key, string fallback, string description = null) {
        description ??= $"{Name} - {fallback}";
        return Loc.Localize($"{this.Key} / {key}", fallback, $"[{this.GetType().Name}] {description}");
    }

    public string LocString(string keyAndFallback) {
        return LocString(keyAndFallback, keyAndFallback);
    }

    private void DrawCommon() {
        if (hasPreviewImage) {
            ImGui.SameLine();
            ImGuiExt.IconButton($"##previewButton", FontAwesomeIcon.Image);
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                if (previewImage == null) {
                    try {
                        previewImage = PluginInterface.UiBuilder.LoadImage(Path.Join(PluginInterface.AssemblyLocation.DirectoryName, "TweakPreviews", $"{Key}.png"));
                    } catch {
                        hasPreviewImage = false;
                    }
                } else {
                    ImGui.Image(previewImage.ImGuiHandle, new Vector2(previewImage.Width, previewImage.Height));
                }
                ImGui.EndTooltip();
            }
        }
        
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
            ImGui.SetCursorPosX(Math.Max(minPos, ImGuiExt.GetWindowContentRegionSize().X - size.X));
            ImGui.TextDisabled(text);
            if (ImGui.IsItemHovered()) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked()) {
                ImGui.SetClipboardText(Key);
            }
        }

        if (TweakManager is { Enabled: false }) { 
            ImGui.TextColored(ImGuiColors.DalamudRed, $"\tThis tweak is part of {TweakManager.Name}. Enable it in General Options.");
        }
    }

    protected T LoadConfig<T>() where T : TweakConfig => LoadConfig<T>(this.Key);

    protected T LoadConfig<T>(string key) where T : TweakConfig {
        try {
            var configDirectory = PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, key + ".json");
            if (!File.Exists(configFile)) return default;
            var jsonString = File.ReadAllText(configFile);
            return JsonConvert.DeserializeObject<T>(jsonString);
        } catch (Exception ex) {
            SimpleLog.Error($"Failed to load config for tweak: {Name}");
            SimpleLog.Error(ex);
            return default;
        }
    }
    
    private object LoadConfig(Type T, string key) {
        if (!T.IsSubclassOf(typeof(TweakConfig))) throw new Exception($"{T} is not a TweakConfig class.");
        try {
            var configDirectory = PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, key + ".json");
            if (!File.Exists(configFile)) return default;
            var jsonString = File.ReadAllText(configFile);
            return JsonConvert.DeserializeObject(jsonString, T);
        } catch (Exception ex) {
            SimpleLog.Error($"Failed to load config for tweak: {Name}");
            SimpleLog.Error(ex);
            return null;
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
    
    private void SaveConfig(object config) {
        try {
            if (!config.GetType().IsSubclassOf(typeof(TweakConfig))) {
                SimpleLog.Error($"Failed to save Config: {config.GetType().Name} is not a subclass of TweakConfig.");
                return;
            }
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
        var shouldForceOpenConfig = ForceOpenConfig;
        ForceOpenConfig = false;
        var configTreeOpen = false;
        if ((this is CommandTweak || UseAutoConfig || DrawConfigTree != null) && (Enabled || this is CommandTweak)) {
            var x = ImGui.GetCursorPosX();
            if (shouldForceOpenConfig) ImGui.SetNextItemOpen(true);
            if (ImGui.TreeNode($"{LocalizedName}##treeConfig_{GetType().Name}")) {
                configTreeOpen = true;
                DrawCommon();
                ImGui.SetCursorPosX(x);
                ImGui.BeginGroup();
                if (Enabled && UseAutoConfig) DrawAutoConfig(ref hasChanged);
                if (Enabled) DrawConfigTree?.Invoke(ref hasChanged);
                if (this is CommandTweak ct) {
                    if (Enabled && (UseAutoConfig || DrawConfigTree != null)) ImGui.Text("Customize Commands:");
                    using var _ = ImRaii.PushIndent(condition: Enabled && (UseAutoConfig || DrawConfigTree != null));
                    ct.DrawCommandEditor(false);
                }

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

        if (hasChanged && Enabled) ConfigChanged();
        return configTreeOpen;
    }

    protected virtual void ConfigChanged() { }

    public virtual void LanguageChanged() { }

    private void DrawAutoConfig(ref bool hasChanged) {
        try {
            var configProperty = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig)));
            if (configProperty == null) {
                ImGui.Text("No Config Property Found");
                return;
            }
            var configObj = configProperty.GetValue(this);

            if (configObj == null) {
                configObj = Activator.CreateInstance(configProperty.PropertyType);
                configProperty.SetValue(this, configObj);
            }

            var fields = configObj.GetType().GetFields()
                .Where(f => f.GetCustomAttribute(typeof(TweakConfigOptionAttribute)) != null)
                .Select(f => (f, (TweakConfigOptionAttribute) f.GetCustomAttribute(typeof(TweakConfigOptionAttribute))))
                .OrderBy(a => a.Item2.Priority).ThenBy(a => a.Item2.Name);

            var configOptionIndex = 0;
            foreach (var (f, attr) in fields) {
                if (attr.ConditionalDisplay) {
                    var conditionalMethod = configObj.GetType().GetMethod($"ShouldShow{f.Name}", BindingFlags.Public | BindingFlags.Instance);
                    if (conditionalMethod != null) {
                        var shouldShow = (bool) (conditionalMethod.Invoke(configObj, Array.Empty<object?>()) ?? true);
                        if (!shouldShow) continue;
                    }
                }


                if (attr.SameLine) ImGui.SameLine();

                var localizedName = LocString(attr.LocalizeKey, attr.Name, $"[Config] {attr.Name}");
                if (attr.Editor != null) {
                    var v = f.GetValue(configObj);
                    var arr = new [] {$"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", v};
                    var o = (bool) attr.Editor.Invoke(null, arr);
                    if (o) {
                        hasChanged = true;
                        f.SetValue(configObj, arr[1]);
                    }
                } else if (f.FieldType == typeof(bool)) {
                    var v = (bool) f.GetValue(configObj);
                    if (ImGui.Checkbox($"{localizedName}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v)) {
                        hasChanged = true;
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
                        
                    if (attr.EnforcedLimit && v < attr.IntMin) {
                        v = attr.IntMin;
                        e = true;
                    }

                    if (attr.EnforcedLimit &&v > attr.IntMax) {
                        v = attr.IntMax;
                        e = true;
                    }
                        
                    if (e) {
                        f.SetValue(configObj, v);
                        hasChanged = true;
                    }
                } else if (f.FieldType.IsEnum) {
                    var v = (Enum)f.GetValue(configObj);

                    if (attr.EditorSize != int.MinValue) ImGui.SetNextItemWidth(attr.EditorSize == -1 ? -1 : attr.EditorSize * ImGui.GetIO().FontGlobalScale);
                    
                    if (ImGui.BeginCombo($"{localizedName}##{f.Name}_{this.GetType().Namespace}_{configOptionIndex++}", $"{v.GetDescription()}")) {
                        foreach (var eV in f.FieldType.GetEnumValues()) {
                            if (eV is not Enum enumValue) {
                                ImGui.Selectable($"???{eV}");
                                continue;
                            }
                            if (ImGui.Selectable($"{enumValue.GetDescription()}", v.Equals(enumValue))) {
                                f.SetValue(configObj, enumValue);
                            }
                        }

                        ImGui.EndCombo();
                    }
                } else {
                    ImGui.Text($"Invalid Auto Field Type: {f.Name}");
                }

            }

        } catch (Exception ex) {
            ImGui.Text($"Error with AutoConfig: {ex.Message}");
            ImGui.TextWrapped($"{ex.StackTrace}");
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
    protected virtual DrawConfigDelegate DrawConfigTree { get; private set; }


    private void AttemptDrawConfigSetup() {
        if (DrawConfigTree != null) return;
        
        var method = GetType().GetMethod("DrawConfig", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;

        if (method.ReturnType != typeof(void)) {
            Plugin.Error(this, new Exception("Failed to set DrawConfig function. Return type not void."), true);
            return;
        }
        
        var parameters = method.GetParameters();

        if (parameters.Length == 0) {

            DrawConfigTree = (ref bool changed) => {
                method.Invoke(this, null);
            };
            return;

        }
        
        if (parameters.Length == 1) {
            var param = parameters[0];

            if (param.ParameterType.IsByRef) {
                var refType = param.ParameterType.GetElementType();

                if (refType == typeof(bool)) {
                    DrawConfigTree = (ref bool changed) => {
                        var o = new object[] { changed };
                        method.Invoke(this, o);
                        changed = (bool)o[0];
                    };
                    return;
                }
            }

        } 
        
        Plugin.Error(this, new Exception("Failed to set DrawConfig function. Invalid parameters."), true);
    }
    
    public virtual void Setup() {
        hasPreviewImage = File.Exists(Path.Join(PluginInterface.AssemblyLocation.DirectoryName, "TweakPreviews", $"{Key}.png"));

        foreach (var c in GetType().GetCustomAttributes<ChangelogAttribute>()) {
            if (c is TweakReleaseVersionAttribute) {
                AddChangelogNewTweak(c.Version);
            }

            foreach (var change in c.Changes) {
                AddChangelog(c.Version, change).Author(c.Author);
            }
        }

        AttemptDrawConfigSetup();
        Ready = true;
    }

    private bool signatureHelperInitialized = false;

    private void AutoLoadConfig() {
        SimpleLog.Verbose($"[{Key}] AutoLoading Config");
        var configProperty = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig)));
        if (configProperty == null) {
            SimpleLog.Error("Failed to AutoLoad config. No TweakConfig property found.");
            return;
        }

        var config = LoadConfig(configProperty.PropertyType, TweakAutoConfigAttribute.ConfigKey ?? Key);
        if (config == null) {
            config = Activator.CreateInstance(configProperty.PropertyType);
            configProperty.SetValue(this, config);
        } else {
            configProperty.SetValue(this, config);
        }
    }

    private void AutoSaveConfig() {
        SimpleLog.Verbose($"[{Key}] AutoSaving Config");
        var configProperty = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(TweakConfig)));
        if (configProperty == null) {
            SimpleLog.Error("Failed to AutoSave config. No TweakConfig property found.");
            return;
        }
        var config = configProperty.GetValue(this);
        if (config == null) return;
        SaveConfig(config);
    }
    
    
    internal void InternalEnable() {
        Unloading = false;
        if (!signatureHelperInitialized) {
            SignatureHelper.Initialise(this);
            signatureHelperInitialized = true;
        }
        
        // Auto Load Config
        if (UseAutoConfig && TweakAutoConfigAttribute is not NoAutoConfig && TweakAutoConfigAttribute.AutoSaveLoad) {
           AutoLoadConfig(); 
        }

        Enable();
        EventController.RegisterEvents(this);
        
        foreach (var (field, attribute) in this.GetFieldsWithAttribute<TweakHookAttribute>()) {
            if (!attribute.AutoEnable) continue;
            SimpleLog.Verbose($"Enable Tweak Hook: [{Name}] {field.Name}");
            if (field.GetValue(this) is IHookWrapper h) {
                h.Enable();
            }
        }
        
        Enabled = true;
    }

    protected virtual void Enable() {
        
    }

    internal void InternalDisable() {
        Unloading = true;
        Disable();
        EventController.UnregisterEvents(this);

        foreach (var (field, _) in this.GetFieldsWithAttribute<TweakHookAttribute>()) {
            SimpleLog.Verbose($"Disable Tweak Hook: [{Name}] {field.Name}");
            if (field.GetValue(this) is IHookWrapper h) {
                h.Disable();
            }
        }

        // Auto Save Config
        if (UseAutoConfig && TweakAutoConfigAttribute is not NoAutoConfig && TweakAutoConfigAttribute.AutoSaveLoad) {
            AutoSaveConfig(); 
        }
        Enabled = false;
    }

    protected virtual void Disable() {
        
    }

    public virtual void Dispose() {
        foreach (var (field, _) in this.GetFieldsWithAttribute<TweakHookAttribute>()) {
            SimpleLog.Verbose($"Dispose Tweak Hook: [{Name}] {field.Name}");
            if (field.GetValue(this) is IHookWrapper h) {
                h.Dispose();
            }
        }
        Ready = false;
    }

    internal void InternalDispose() {
        previewImage?.Dispose();
        Dispose();
        IsDisposed = true;
    }

    protected ChangelogEntry AddChangelog(string version, string log) => Changelog.Add(this, version, log);
    protected ChangelogEntry AddChangelogNewTweak(string version) => Changelog.AddNewTweak(this, version).Author(Author);


    #region Attribute Handles

    private TweakNameAttribute tweakNameAttribute;
    protected TweakNameAttribute TweakNameAttribute {
        get {
            if (tweakNameAttribute != null) return tweakNameAttribute;
            tweakNameAttribute = GetType().GetCustomAttribute<TweakNameAttribute>() ?? new TweakNameAttribute($"{GetType().Name}");
            return tweakNameAttribute;
        }
    }
    
    private TweakDescriptionAttribute tweakDescriptionAttribute;
    protected TweakDescriptionAttribute TweakDescriptionAttribute {
        get {
            if (tweakDescriptionAttribute != null) return tweakDescriptionAttribute;
            tweakDescriptionAttribute = GetType().GetCustomAttribute<TweakDescriptionAttribute>() ?? TweakDescriptionAttribute.Default;
            return tweakDescriptionAttribute;
        }
    }
    
    private TweakAuthorAttribute tweakAuthorAttribute;
    protected TweakAuthorAttribute TweakAuthorAttribute {
        get {
            if (tweakAuthorAttribute != null) return tweakAuthorAttribute;
            tweakAuthorAttribute = GetType().GetCustomAttribute<TweakAuthorAttribute>() ?? TweakAuthorAttribute.Default;
            return tweakAuthorAttribute;
        }
    }

    private TweakVersionAttribute tweakVersionAttribute;
    protected TweakVersionAttribute TweakVersionAttribute {
        get {
            if (tweakVersionAttribute != null) return tweakVersionAttribute;
            tweakVersionAttribute = GetType().GetCustomAttribute<TweakVersionAttribute>() ?? new TweakVersionAttribute(1);
            return tweakVersionAttribute;
        }
    }

    private TweakAutoConfigAttribute tweakAutoConfigAttribute;

    protected TweakAutoConfigAttribute TweakAutoConfigAttribute {
        get {
            if (tweakAutoConfigAttribute != null) return tweakAutoConfigAttribute;
            tweakAutoConfigAttribute = GetType().GetCustomAttribute<TweakAutoConfigAttribute>() ?? NoAutoConfig.Singleton;
            return tweakAutoConfigAttribute;
        }
    }

    private HashSet<string> categories;
    public HashSet<string> Categories {
        get {
            if (categories != null) return categories;

            void HandleAttributes(IEnumerable<TweakCategoryAttribute> attributes) {
                categories = new HashSet<string>();
                foreach (var attr in attributes) {
                    foreach (var v in attr.Categories) {
                        categories.Add(v);
                    }
                }
            }
            
            HandleAttributes(GetType().GetCustomAttributes<TweakCategoryAttribute>(true));
            foreach (var i in GetType().GetInterfaces()) {
                HandleAttributes(i.GetCustomAttributes<TweakCategoryAttribute>(true));
            }

            if (Experimental) categories.Add($"{TweakCategory.Experimental}");
            
            return categories;
        }
    }
    #endregion
    

}