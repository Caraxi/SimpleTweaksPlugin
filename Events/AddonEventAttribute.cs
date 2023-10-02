using System;
using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class AddonEventAttribute : EventAttribute {
    public AddonEvent Event { get; }
    public string[] AddonNames { get; }

    public AddonEventAttribute(AddonEvent @event, params string[] addonNames) {
        Event = @event;
        AddonNames = addonNames;
    }
}
