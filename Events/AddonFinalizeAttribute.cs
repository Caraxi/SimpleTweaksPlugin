using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

public class AddonFinalizeAttribute : AddonEventAttribute {
    public AddonFinalizeAttribute(params string[] addonNames) : base(AddonEvent.PreFinalize, addonNames) { }
}

