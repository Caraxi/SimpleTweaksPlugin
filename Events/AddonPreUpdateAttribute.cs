using Dalamud.Game.Addon.Lifecycle;

namespace SimpleTweaksPlugin.Events; 

public class AddonPostUpdateAttribute : AddonEventAttribute {
    public AddonPostUpdateAttribute(params string[] addonNames) : base(AddonEvent.PostRequestedUpdate, addonNames) { }
}
