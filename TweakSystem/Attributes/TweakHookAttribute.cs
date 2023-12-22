using System;
using JetBrains.Annotations;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Field)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakHookAttribute : Attribute {
    public bool AutoEnable { get; init; } = true;
}
