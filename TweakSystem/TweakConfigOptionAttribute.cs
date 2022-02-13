using System;
using System.Reflection;

namespace SimpleTweaksPlugin.TweakSystem; 

public class TweakConfigOptionAttribute : Attribute {
        
    public string Name { get; }

    public string LocalizeKey { get; }

    public int Priority { get; } = 0;
    public int EditorSize { get; set; } = -1;

    public bool SameLine { get; set; } = false;

    public bool ConditionalDisplay { get; set; } = false;

    // Int 
    public int IntMin { get; set; } = int.MinValue;
    public int IntMax { get; set; } = int.MaxValue;
    public IntEditType IntType { get; set; } = IntEditType.Slider;

    public bool EnforcedLimit { get; set; } = true;

    public delegate bool ConfigOptionEditor(string name, ref object configOption);
        
    public MethodInfo Editor { get; set; }

    public enum IntEditType {
        Slider,
        Drag,
    }
        
    public TweakConfigOptionAttribute(string name, int priority = 0, string localizeKey = null) {
        Name = name;
        LocalizeKey = localizeKey ?? name;
        Priority = priority;
    }

    public TweakConfigOptionAttribute(string name, string editorType, int priority = 0, string localizeKey = null) {
        Name = name;
        Priority = priority;
        LocalizeKey = localizeKey ?? name;
        Editor = typeof(TweakConfigEditor).GetMethod($"{editorType}Editor", BindingFlags.Public | BindingFlags.Static);
    }
        
}