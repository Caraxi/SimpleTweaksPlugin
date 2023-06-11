using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public class DutyListBackground : UiAdjustments.SubTweak, IDisabledTweak
{
    public override string Name => "Duty List Background";
    public override string Description => "Adds a configurable background to the Duty List";
    protected override string Author => "MidoriKami";
    
    public override void Setup()
    {
        if (Ready) return;

        AddChangelogNewTweak("1.8.7.0");
        AddChangelog("1.8.7.1", "Improved tweak stability.");
        AddChangelog("1.8.7.3", "Prevent crash when using Aestetician.");
        
        Ready = true;
    }

    public string DisabledMessage => "This tweak causes game crashes in specific circumstances while in the Inn Room.";
}