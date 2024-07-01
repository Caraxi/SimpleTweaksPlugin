using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Objects.Types;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakCategory(TweakCategory.Command)]
public class FixTarget : Tweak {
    public override string Name => "Fix '/target' command";
    public override string Description => "Allows using the default '/target' command for targeting players or NPCs by their names.";

    private Regex regex;

    public override void Setup() {
        AddChangelog("1.8.3.0", "Fixed tweak not working in french.").Author("Aireil");
        base.Setup();
    }

    protected override void Enable() {

        regex = Service.ClientState.ClientLanguage switch {
            ClientLanguage.Japanese => new Regex(@"^\d+?番目のターゲット名の指定が正しくありません。： (.+)$"),
            ClientLanguage.German => new Regex(@"^Der Unterbefehl \[Name des Ziels\] an der \d+\. Stelle des Textkommandos \((.+)\) ist fehlerhaft\.$"),
            ClientLanguage.French => new Regex(@"^Le \d+er? argument “nom de la cible” est incorrect \((.*?)\)\.$"),
            ClientLanguage.English => new Regex(@"^“(.+)” is not a valid target name\.$"),
            _ => null
        };

        Service.Chat.CheckMessageHandled += OnChatMessage;

        base.Enable();
    }

    protected override void Disable() {
        Service.Chat.CheckMessageHandled -= OnChatMessage;
        base.Disable();
    }

    private unsafe void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        if (Common.LastCommand == null || Common.LastCommand->StringPtr == null) return;
        var lastCommandStr = Encoding.UTF8.GetString(Common.LastCommand->StringPtr, (int) Common.LastCommand->BufUsed);
        if (!(lastCommandStr.StartsWith("/target ") || lastCommandStr.StartsWith("/ziel ") || lastCommandStr.StartsWith("/cibler "))) {
            return;
        }

        var match = regex.Match(message.TextValue);
        if (!match.Success) return;
        var searchName = match.Groups[1].Value.ToLowerInvariant();

        IGameObject closestMatch = null;
        var closestDistance = float.MaxValue;
        var player = Service.ClientState.LocalPlayer;
        foreach (var actor in Service.Objects) {

            if (actor == null) continue;
            if (actor.Name.TextValue.ToLowerInvariant().Contains(searchName) &&
                ((GameObjectStruct*)actor.Address)->GetIsTargetable()) {
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
            Service.Targets.Target = closestMatch;
        }
    }
}
