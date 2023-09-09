using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakNameAttribute : Attribute {
    public string Name { get; }
    public TweakNameAttribute(string name) {
        Name = name;
    }
}
