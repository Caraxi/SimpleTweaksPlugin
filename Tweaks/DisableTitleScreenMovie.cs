using Dalamud.Game.Internal;
using FFXIVClientInterface.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    internal unsafe class DisableTitleScreenMovie : Tweak {
        public override string Name => "Disable Title Screen Movie";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }
        
        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            base.Disable();
        }

        private void FrameworkUpdate(Framework framework) {
            try {
                if (PluginInterface.ClientState.LocalContentId == 0) {
                    SimpleTweaksPlugin.Client.UiModule.AgentModule.GetAgent<AgentLobby>().Data->IdleTime = 0;
                }
            } catch {
                // Ignored
            }
        }
    }
}
