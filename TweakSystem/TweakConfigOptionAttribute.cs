using System;

namespace SimpleTweaksPlugin.TweakSystem {
    public class TweakConfigOptionAttribute : Attribute {
        
        public string Name { get; }
        public int Priority { get; } = 0;
        public int EditorSize { get; set; } = -1;
        
        // Int 
        public int IntMin { get; set; } = int.MinValue;
        public int IntMax { get; set; } = int.MaxValue;
        public IntEditType IntType { get; set; } = IntEditType.Slider;

        public enum IntEditType {
            Slider,
        }
        
        public TweakConfigOptionAttribute(string name, int priority = 0) {
            Name = name;
            Priority = priority;
        }
    }
}
