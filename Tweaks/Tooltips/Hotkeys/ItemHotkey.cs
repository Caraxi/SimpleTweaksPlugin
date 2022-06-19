using System;
using System.IO;
using Dalamud.Game.ClientState.Keys;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using SimpleTweaksPlugin.Sheets;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys; 

public abstract class ItemHotkey : IDisposable {
    public abstract string Name { get; }
    public string LocalizedName => LocString("Name", Name, "Tweak Name");

    protected abstract VirtualKey[] DefaultKeyCombo { get; }

    public VirtualKey[] Hotkey {
        get => Config.Key ?? DefaultKeyCombo;
        set {
            Config.Key = value;
            SaveConfig();
        }
    }
    
    public virtual string Key => GetType().Name;

    public virtual string HintText => Name;

    private Type configType = typeof(ItemHotkeyConfig);

    public ItemHotkeyConfig Config;
    
    public bool Enabled { get; private set; }

    public virtual bool AcceptsEventItem => false;
    public virtual bool AcceptsNormalItem => true;
    
    
    
    public class ItemHotkeyConfig {
        public virtual int Version { get; set; } = 1;

        public bool Enabled = false;
        public bool HideFromTooltip = false;
        public VirtualKey[] Key;

    }
    
    public void Enable(bool fromTweakEnable = false) {
        var cType = this.GetType().GetNestedType("HotkeyConfig");
        if (cType != null && cType.IsSubclassOf(typeof(ItemHotkeyConfig))) {
            configType = cType;
        }
        
        LoadConfig();
        if (fromTweakEnable && Config.Enabled == false) return;
        Config.Enabled = true;
        if (!Enabled) OnEnable();
        Enabled = true;
        if (!fromTweakEnable) SaveConfig();
    }

    public void Disable(bool fromTweakDisable = false) {
        var wasEnabled = Enabled;
        Enabled = false;
        if (wasEnabled) OnDisable();
        if (!fromTweakDisable) Config.Enabled = false;
        SaveConfig();
    }
    
    protected virtual void OnEnable() {
        
    }

    protected virtual void OnDisable() {
        
    }

    public virtual void OnTriggered(ExtendedItem item) { }
    public virtual void OnTriggered(EventItem item) { }

    public virtual bool DoShow(ExtendedItem item) => AcceptsNormalItem;
    public virtual bool DoShow(EventItem item) => AcceptsEventItem;

    public virtual void DrawExtraConfig() { }

    public virtual void Dispose() {
        
    }

    public void LoadConfig() {
        try {
            Config = (ItemHotkeyConfig) Activator.CreateInstance(configType);
            var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, $"{nameof(TooltipTweaks)}@{nameof(ItemHotkeys)}.{Key}.json");
            if (File.Exists(configFile)) {
                var jsonString = File.ReadAllText(configFile);
                Config = (ItemHotkeyConfig) JsonConvert.DeserializeObject(jsonString, configType);
            }
        } catch (Exception ex) {
            Config = (ItemHotkeyConfig) Activator.CreateInstance(configType);
            SimpleLog.Error(ex);
        }
    }
    
    public void SaveConfig() {
        try {
            if (Config == null) return;
            var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, $"{nameof(TooltipTweaks)}@{nameof(ItemHotkeys)}.{Key}.json");
            var jsonString = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(configFile, jsonString);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
    
    public string LocString(string key, string fallback, string description = null) {
        description ??= $"Item Hotkey : {Name} - {fallback}";
        return Loc.Localize($"{nameof(TooltipTweaks)}@{nameof(ItemHotkeys)}.{this.Key} / {key}", fallback, $"[Item Hotkey - {this.GetType().Name}] {description}");
    }

    public string LocString(string keyAndFallback) {
        return LocString(keyAndFallback, keyAndFallback);
    }
}
