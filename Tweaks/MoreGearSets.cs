using System;
using Dalamud;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class MoreGearSets : Tweak, IDisabledTweak {
    public override string Name => "More Gear Sets";
    public override string Description => "Increases maximum gear sets to 100.";
    protected override string Author => "UnknownX";
    public string DisabledMessage => "This tweak was implemented into the base game as of 6.4";
}