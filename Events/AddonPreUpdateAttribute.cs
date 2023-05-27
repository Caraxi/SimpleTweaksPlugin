using System;

namespace SimpleTweaksPlugin.Events; 

public class AddonPostUpdateAttribute : EventAttribute {
    public string[] AddonNames { get; }
    
    public AddonPostUpdateAttribute(params string[] addonNames) {
        AddonNames = addonNames;
    }
}
