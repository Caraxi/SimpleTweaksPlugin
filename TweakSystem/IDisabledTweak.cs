namespace SimpleTweaksPlugin.TweakSystem; 

#if DEBUG
[TweakCategory(TweakCategory.Disabled)]
#endif
public interface IDisabledTweak {
    public string DisabledMessage { get; }
}
