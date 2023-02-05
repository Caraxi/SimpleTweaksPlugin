namespace SimpleTweaksPlugin.TweakSystem; 

public class BlacklistedTweak : BaseTweak, IDisabledTweak {
    public override string Name { get; }
    public override string Key { get; }
    public string DisabledMessage { get; }
    
    public BlacklistedTweak(string key, string name, string disabledMessage) {
        Key = key;
        Name = name;
        DisabledMessage = disabledMessage;
    }
}

