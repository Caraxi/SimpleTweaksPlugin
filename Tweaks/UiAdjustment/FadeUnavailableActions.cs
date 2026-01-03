using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Fade Unavailable Actions")]
[TweakDescription("Instead of darkening icons, makes them transparent when unavailable.")]
[TweakAuthor("MidoriKami")]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.3.1")]
[TweakVersion(2)]
[Changelog("1.8.3.2", "Tweak now only applies to the icon image itself and not the entire button")]
[Changelog("1.8.3.2", "Add option to apply transparency to the slot frame of the icon")]
[Changelog("1.8.3.2", "Add option to apply to sync'd skills only")]
[Changelog("1.8.4.0", "Tweak now only applies to combat actions")]
[Changelog("1.8.4.0", "Properly resets hotbar state on unload/disable")]
[Changelog("1.9.2.0", "Added option to make skills that are out of range red")]
[Changelog("1.14.0.2", "Tweak has been removed. An alternative may be found in the VanillaPlus Plugin")]
public class FadeUnavailableActions : UiAdjustments.SubTweak, IDisabledTweak {
    public string DisabledMessage => "This tweak has been removed. An alternative may be found in the VanillaPlus Plugin.";
}