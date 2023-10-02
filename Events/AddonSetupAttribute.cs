using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

public class AddonSetupAttribute : AddonEventAttribute {
    public AddonSetupAttribute(params string[] addonNames) : base(AddonEvent.PreSetup, addonNames) { }
}
