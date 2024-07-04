using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Chat Tweaks")]
public class ChatTweaks : SubTweakManager<ChatTweaks.SubTweak> {
    public override bool AlwaysEnabled => true;

    [TweakCategory(TweakCategory.Chat)]
    public abstract class SubTweak : BaseTweak {
        public override string Key => $"{nameof(ChatTweaks)}@{base.Key}";
    }
}