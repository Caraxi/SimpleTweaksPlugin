using System;

namespace SimpleTweaksPlugin.TweakSystem; 

public class TweakNameAttribute : Attribute {
    public string Name { get; }
    public TweakNameAttribute(string name) {
        Name = name;
    }
}
