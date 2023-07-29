using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Field)]
public class TweakHookAttribute : Attribute {
    public bool AutoEnable { get; init; } = true;
}
