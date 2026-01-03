using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Loot Window Select Next Item")]
[TweakDescription("Upon pressing 'Need', 'Greed', or 'Pass' automatically select the next loot item.")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.9.2")]
[Changelog("1.14.0.2", "Tweak has been removed. An alternative may be found in the VanillaPlus Plugin")]
public class LootWindowSelectNext : UiAdjustments.SubTweak, IDisabledTweak {
    public string DisabledMessage => "This tweak has been removed. An alternative may be found in the VanillaPlus Plugin.";
}