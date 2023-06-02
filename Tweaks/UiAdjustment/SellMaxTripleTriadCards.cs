using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public class SellMaxTripleTriadCards : UiAdjustments.SubTweak, IDisabledTweak {
    public override string Name => "Default to max when selling Triple Triad Cards";
    public override string Description => "Set the default number of cards to sell at the Triple Triad Trader to the number of cards you have.";
    public string DisabledMessage => "This tweak was implemented into the base game as of 6.4";
}