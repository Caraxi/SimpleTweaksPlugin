using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.ClientState.Actors.Types;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public class FixTarget : Tweak {
        public override string Name => "Fix '/target' command";
        public override string Description => "Allows using the default '/target' command for targeting players or NPCs by their names.";

        private Regex regex;
        
        public override void Enable() {
            
            regex = PluginInterface.ClientState.ClientLanguage switch {
                ClientLanguage.Japanese => new Regex(@"^\d+?番目のターゲット名の指定が正しくありません。： (.+)$"),
                ClientLanguage.German => new Regex(@"^Der Unterbefehl \[Name des Ziels\] an der \d+\. Stelle des Textkommandos \((.+)\) ist fehlerhaft\.$"),
                ClientLanguage.French => new Regex(@"^Le \d+er? argument “nom de la cible” est incorrect (.*?)\.$"), 
                ClientLanguage.English => new Regex(@"^“(.+)” is not a valid target name\.$"),
                _ => null
            };
            
            PluginInterface.Framework.Gui.Chat.OnChatMessage += OnChatMessage;
            
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.Gui.Chat.OnChatMessage -= OnChatMessage;
            base.Disable();
        }
        
        private void OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (type != XivChatType.ErrorMessage) return;
            var match = regex.Match(message.TextValue);
            if (!match.Success) return;
            var searchName = match.Groups[1].Value.ToLowerInvariant();

            Actor closestMatch = null;
            var closestDistance = float.MaxValue;
            var player = Plugin.PluginInterface.ClientState.LocalPlayer;
            foreach (var actor in PluginInterface.ClientState.Actors) {
                
                if (actor == null) continue;
                if (actor.Name.ToLowerInvariant().Contains(searchName)) {
                    var distance = Vector3.Distance(player.Position, actor.Position);
                    if (closestMatch == null) {
                        closestMatch = actor;
                        closestDistance = distance;
                        continue;
                    }

                    if (closestDistance > distance) {
                        closestMatch = actor;
                        closestDistance = distance;
                    }
                }
            }

            if (closestMatch != null) {
                isHandled = true;
                PluginInterface.ClientState.Targets.SetCurrentTarget(closestMatch);
            }
        }
    }
}
