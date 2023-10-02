namespace SimpleTweaksPlugin.Events; 

public class AddonPreSetupAttribute : EventAttribute {
    public string[] AddonNames { get; }
    
    public AddonPreSetupAttribute(params string[] addonNames) {
        AddonNames = addonNames;
    }
}
