using System;
using System.Reflection;
using JetBrains.Annotations;

namespace SimpleTweaksPlugin.TweakSystem; 

[UsedImplicitly(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
[AttributeUsage(AttributeTargets.Field)]
public class TweakConfigOptionAttribute : Attribute {
        
    public string Name { get; }

    public string LocalizeKey { get; }

    public string HelpText { get; init; } = string.Empty;
    
    public int Priority { get; }
    public int EditorSize { get; set; } = int.MinValue;

    public bool SameLine { get; set; } = false;

    public bool ConditionalDisplay { get; set; } = false;

    // Int 
    public int IntMin { get; set; } = int.MinValue;
    public int IntMax { get; set; } = int.MaxValue;
    public IntEditType IntType { get; set; } = IntEditType.Slider;

    public bool EnforcedLimit { get; set; } = true;

    public delegate bool ConfigOptionEditor(string name, ref object configOption);
        
    public MethodInfo? Editor { get; set; }

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