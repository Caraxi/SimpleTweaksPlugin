using System;

namespace SimpleTweaksPlugin.TweakSystem;

[AttributeUsage(AttributeTargets.Field)]
public class EnumTooltipAttribute(string text) : Attribute {
    public string Text => text;
}
