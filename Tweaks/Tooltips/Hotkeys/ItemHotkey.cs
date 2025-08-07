using System;
using System.IO;
using Dalamud.Game.ClientState.Keys;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace SimpleTweaksPlugin.Tweaks.Tooltips.Hotkeys;

public abstract class ItemHotkey : IDisposable {
    protected abstract string Name { get; }
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

        public bool Enabled;
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

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() { }

    public virtual void OnTriggered(Item item) { }
    public virtual void OnTriggered(EventItem item) { }

    public virtual bool DoShow(Item item) => AcceptsNormalItem;

    public virtual bool DoShow(EventItem eventItem) => AcceptsEventItem;

    public virtual void DrawExtraConfig() { }

    public virtual void Dispose() { }

    public void LoadConfig() {
        try {
            Config = (ItemHotkeyConfig)Activator.CreateInstance(configType);
            var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, $"{nameof(TooltipTweaks)}@{nameof(ItemHotkeys)}.{Key}.json");
            if (File.Exists(configFile)) {
                var jsonString = File.ReadAllText(configFile);
                Config = (ItemHotkeyConfig)JsonConvert.DeserializeObject(jsonString, configType);
            }

            Config ??= (ItemHotkeyConfig)Activator.CreateInstance(configType);
        } catch (Exception ex) {
            Config = (ItemHotkeyConfig)Activator.CreateInstance(configType);
            SimpleLog.Error(ex);
        }
    }

    public void SaveConfig() {
        try {
            if (Config == null) return;
            var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, $"{nameof(TooltipTweaks)}@{nameof(ItemHotkeys)}.{Key}.json");
            var jsonString = JsonConvert.SerializeObject(Config, Formatting.Indented);
#if !TEST
            File.WriteAllText(configFile, jsonString);
#endif
            
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

    public void OnTriggered(EventItem? item) {
        if (item != null) OnTriggered(item.Value);
    }

    public void OnTriggered(Item? item) {
        if (item != null) OnTriggered(item.Value);
    }

    public bool DoShow(EventItem? item) => item != null && DoShow(item.Value);
    public bool DoShow(Item? item) => item != null && DoShow(item.Value);
}
