using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakVersionAttribute : Attribute {
    public uint Version { get; }
    public TweakVersionAttribute(uint version) {
        Version = version;
    }
}
