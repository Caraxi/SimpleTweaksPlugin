using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

public class AddonPreUpdateAttribute : AddonEventAttribute {
    public AddonPreUpdateAttribute(params string[] addonNames) : base(AddonEvent.PreRequestedUpdate, addonNames) { }
}
