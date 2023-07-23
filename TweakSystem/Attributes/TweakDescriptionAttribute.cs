using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakDescriptionAttribute : Attribute {
    public string Description { get; }
    public TweakDescriptionAttribute(string description) {
        Description = description;
    }

    private TweakDescriptionAttribute() { }

    public static TweakDescriptionAttribute Default { get; } = new();
}
