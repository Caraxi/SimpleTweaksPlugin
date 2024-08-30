using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakKeyAttribute(string key) : Attribute {
    public string Key { get; } = key;
}
