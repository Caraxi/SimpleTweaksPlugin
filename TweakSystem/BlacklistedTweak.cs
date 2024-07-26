namespace SimpleTweaksPlugin.TweakSystem; 

public class BlacklistedTweak : BaseTweak, IDisabledTweak {
    public override string Key { get; }
    public string DisabledMessage { get; }
    
    public BlacklistedTweak(string key, string name, string disabledMessage) : base(name) {
        Key = key;
        DisabledMessage = disabledMessage;
    }
}

