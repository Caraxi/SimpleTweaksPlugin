using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

public class JokeTweaks : SubTweakManager<JokeTweaks.SubTweak> {
    
    public abstract class SubTweak : BaseTweak {
        public override string Key => $"{nameof(JokeTweaks)}@{base.Key}";
    }
    
    public override string Name => "Joke Tweaks";
}

