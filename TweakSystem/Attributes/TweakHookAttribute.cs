using System;
using JetBrains.Annotations;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Field)]
[UsedImplicitly(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakHookAttribute : Attribute {
    public bool AutoEnable { get; init; } = true;
}
