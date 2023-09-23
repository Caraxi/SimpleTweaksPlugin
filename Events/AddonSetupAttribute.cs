namespace SimpleTweaksPlugin.Events; 

public class AddonSetupAttribute : EventAttribute {
    public string[] AddonNames { get; }
    
    public AddonSetupAttribute(params string[] addonNames) {
        AddonNames = addonNames;
    }
}
