using System;

namespace SimpleTweaksPlugin.TweakSystem;

[AttributeUsage(AttributeTargets.Class)]
public class RequiredClientStructsVersionAttribute(ushort minVersion, ushort maxVersion = ushort.MaxValue) : Attribute {
    public uint MinVersion { get; } = minVersion;
    public uint MaxVersion { get; } = maxVersion;
}
