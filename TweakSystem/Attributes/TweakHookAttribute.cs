using System;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Field)]
public class TweakHookAttribute : Attribute {
    public string Signature { get; init; }
    public bool AutoEnable { get; init; } = true;
    
    public TweakHookAttribute() { }

    public TweakHookAttribute(string signature) {
        this.Signature = signature;
    }
}
