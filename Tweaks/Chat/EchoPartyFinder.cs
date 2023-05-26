#nullable enable
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public class EchoPartyFinder : ChatTweaks.SubTweak, IDisabledTweak
{
    public override string Name => "Echo Party Finder";
    public override string Description => "Prints Party Finder description to chat upon joining a group.";
    protected override string Author => "MidoriKami";
    public string DisabledMessage => "This tweak was implemented into the base game as of 6.4";
}