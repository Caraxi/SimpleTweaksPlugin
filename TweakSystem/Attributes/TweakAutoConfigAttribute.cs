using System;

namespace SimpleTweaksPlugin.TweakSystem;

public class TweakAutoConfigAttribute : Attribute {
    public bool AutoSaveLoad { get; }
    public string? ConfigKey { get; init; } = null;

    public bool SaveOnChange { get; init; }

    public TweakAutoConfigAttribute(bool autoSaveLoad = true, bool saveOnChange = false) {
        AutoSaveLoad = autoSaveLoad;
        SaveOnChange = saveOnChange;
    }
}

internal sealed class NoAutoConfig : TweakAutoConfigAttribute {
    public static NoAutoConfig Singleton { get; } = new();
    private NoAutoConfig() : base(false) { }
}