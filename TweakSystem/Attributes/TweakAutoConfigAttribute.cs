using System;

namespace SimpleTweaksPlugin.TweakSystem;

public class TweakAutoConfigAttribute : Attribute {
    public bool AutoSaveLoad { get; }
    public string ConfigKey { get; init; } = null;
    
    public TweakAutoConfigAttribute(bool autoSaveLoad = true) {
        AutoSaveLoad = autoSaveLoad;
    }
}

internal sealed class NoAutoConfig : TweakAutoConfigAttribute {
    public static NoAutoConfig Singleton { get; } = new();
    private NoAutoConfig() : base(false) { }
}