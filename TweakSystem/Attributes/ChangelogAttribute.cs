using System;
using System.Collections.Generic;

namespace SimpleTweaksPlugin.TweakSystem; 

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ChangelogAttribute : Attribute {
    public string Version { get; }
    public string[] Changes { get; }
    public string Author { get; init; }

    public ChangelogAttribute(string version, params string[] changes) {
        Version = version;
        Changes = changes;
    }
}

public class TweakReleaseVersionAttribute : ChangelogAttribute {
    public TweakReleaseVersionAttribute(string version) : base(version) { }
}
