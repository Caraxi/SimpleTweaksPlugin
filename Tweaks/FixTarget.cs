using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Objects.Types;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SimpleTweaksPlugin.Tweaks;

[TweakCategory(TweakCategory.Command)]
[TweakName("Fix '/target' command")]
[TweakDescription("Allows using the default '/target' command for targeting players or NPCs by their names.")]
[Changelog("1.8.3.0", "Fixed tweak not working in french.", Author = "Aireil")]
[Changelog("1.10.12.6", "Add ability to clear target by providing no name.")]
public class FixTarget : Tweak {
    private Regex? regex;

    protected override void Enable() {
        regex = Service.ClientState.ClientLanguage switch {
            ClientLanguage.Japanese => new Regex(@"^\d+?番目のターゲット名の指定が正しくありません。： (.+)$"),
            ClientLanguage.German => new Regex(@"^Der Unterbefehl \[Name des Ziels\] an der \d+\. Stelle des Textkommandos \((.+)\) ist fehlerhaft\.$"),
            ClientLanguage.French => new Regex(@"^Le \d+er? argument “nom de la cible” est incorrect \((.*?)\)\.$"),
            ClientLanguage.English => new Regex(@"^“(.+)” is not a valid target name\.$"),
            _ => null
        };

        Service.Chat.CheckMessageHandled += OnChatMessage;
    }

    protected override void Disable() {
        Service.Chat.CheckMessageHandled -= OnChatMessage;
    }

    private unsafe void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (regex == null) return;
        if (type != XivChatType.ErrorMessage) return;
        if (Common.LastCommand == null || Common.LastCommand->StringPtr.Value == null) return;
        var lastCommandStr = Common.LastCommand->ToString();
        if (lastCommandStr.Equals("/target") || lastCommandStr.Equals("/ziel") || lastCommandStr.Equals("/cibler")) {
            // Clear target
            Service.Targets.Target = null;
            Service.Targets.SoftTarget = null;
            isHandled = true;
            return;
        }

        if (!(lastCommandStr.StartsWith("/target ") || lastCommandStr.StartsWith("/ziel ") || lastCommandStr.StartsWith("/cibler "))) {
            return;
        }

        var match = regex.Match(message.TextValue);
        if (!match.Success) return;
        var searchName = match.Groups[1].Value.ToLowerInvariant();

        IGameObject? closestMatch = null;
        var closestDistance = float.MaxValue;
        var player = Service.Objects.LocalPlayer;
        if (player == null) return;
        foreach (var actor in Service.Objects) {
            if (!actor.Name.TextValue.Contains(searchName, System.StringComparison.InvariantCultureIgnoreCase) || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;
            var distance = Vector3.Distance(player.Position, actor.Position);
            if (closestMatch == null) {
                closestMatch = actor;
                closestDistance = distance;
                continue;
            }

            if (!(closestDistance > distance)) continue;
            closestMatch = actor;
            closestDistance = distance;
        }

        if (closestMatch == null) return;
        isHandled = true;
        
        Service.Targets.Target = closestMatch;
    }
}
