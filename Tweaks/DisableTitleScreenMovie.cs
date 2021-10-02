using Dalamud.Game;
using Dalamud.Game.Internal;
using FFXIVClientInterface.Client.UI.Agent;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    internal unsafe class DisableTitleScreenMovie : Tweak {
        public override string Name => "Disable Title Screen Movie";
        public override string Description => "Prevents the title screen from playing the introduction movie after 60 seconds.";

        public override void Enable() {
            Service.Framework.Update += FrameworkUpdate;
            base.Enable();
        }
        
        public override void Disable() {
            Service.Framework.Update -= FrameworkUpdate;
            base.Disable();
        }

        private void FrameworkUpdate(Framework framework) {
            try {
                if (Service.Condition == null) return;
                if (Service.Condition.Any()) return;
                SimpleTweaksPlugin.Client.UiModule.AgentModule.GetAgent<AgentLobby>().Data->IdleTime = 0;
            } catch {
                // Ignored
            }
        }
    }
}
