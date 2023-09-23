namespace SimpleTweaksPlugin.Events; 

public class AddonPreUpdateAttribute : EventAttribute {
    public string[] AddonNames { get; }
    
    public AddonPreUpdateAttribute(params string[] addonNames) {
        AddonNames = addonNames;
    }
}
