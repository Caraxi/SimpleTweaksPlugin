using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Open Adventurer Plate Command")]
[TweakDescription("Adds a command to open adventurer plates.")]
public unsafe class CharaCardCommand : CommandTweak {
    protected override string Command => "playerplate";
    protected override string HelpMessage => "Opens the character card for the selected character.";

    protected override void OnCommand(string arguments) {
        if (string.IsNullOrWhiteSpace(arguments)) {
            Service.Chat.PrintError($"/playerplate <t>");
            return;
        }

        
        var resolve = PronounModule.Instance()->ResolvePlaceholder(arguments, 0, 0);
        if (resolve == null) {
            foreach (var actor in Service.Objects) {
                if (actor == null) continue;
                if (actor.Name.TextValue.Equals(arguments, StringComparison.InvariantCultureIgnoreCase)) {
                    resolve = (GameObject*)actor.Address;
                    break;
                }
            }
        }

        if (resolve != null && resolve->ObjectKind == ObjectKind.Pc && resolve->SubKind == 4) {
            AgentCharaCard.Instance()->OpenCharaCard(resolve);
        }
    }
}
