using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class SecretTweaks : SubTweakManager<SecretTweaks.SubTweak> {
        public override bool AlwaysEnabled => true;

        public abstract class SubTweak : BaseTweak {
            public override string Key => $"{nameof(SecretTweaks)}@{base.Key}";
        }

        public override string Name => "Secret";
    }
}
