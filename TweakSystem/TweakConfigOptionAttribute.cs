using System;

namespace SimpleTweaksPlugin.TweakSystem {
    public class TweakConfigOptionAttribute : Attribute {
        
        public string Name { get; }
        public int Priority { get; } = 0;
        
        public TweakConfigOptionAttribute(string name, int priority = 0) {
            Name = name;
            Priority = priority;
        }
    }
}
