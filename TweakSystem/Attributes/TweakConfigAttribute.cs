using System;
using JetBrains.Annotations;

namespace SimpleTweaksPlugin.TweakSystem;

[AttributeUsage(AttributeTargets.Property)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakConfigAttribute : Attribute;
