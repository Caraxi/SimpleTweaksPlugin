using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

public class AddonPreSetupAttribute : AddonEventAttribute {
    public AddonPreSetupAttribute(params string[] addonNames) : base(AddonEvent.PreSetup, addonNames) { }
}
