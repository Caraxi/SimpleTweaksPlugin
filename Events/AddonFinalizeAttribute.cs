namespace SimpleTweaksPlugin.Events; 

public class AddonFinalizeAttribute : EventAttribute {
    public string[] AddonNames { get; }
    
    public AddonFinalizeAttribute(params string[] addonNames) {
        AddonNames = addonNames;
    }
}

