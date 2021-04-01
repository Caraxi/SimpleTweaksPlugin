using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
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
