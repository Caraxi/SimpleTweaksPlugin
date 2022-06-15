using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public bool ShouldSerializeChatTweaks() => ChatTweaks.DisableChatAutoscroll != null || ChatTweaks.RenameChatTabs != null;
        public ChatTweaksConfig ChatTweaks = new();
    }

    public partial class ChatTweaksConfig { }
}

namespace SimpleTweaksPlugin.Tweaks {
    public class ChatTweaks : SubTweakManager<ChatTweaks.SubTweak> {
        public override bool AlwaysEnabled => true;

        public abstract class SubTweak : BaseTweak {
            public override string Key => $"{nameof(ChatTweaks)}@{base.Key}";
        }

        public override string Name => "Chat Tweaks";
    }
}
