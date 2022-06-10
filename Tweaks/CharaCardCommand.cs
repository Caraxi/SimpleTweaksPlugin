using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class CharaCardCommand : CommandTweak {
    public override string Name => "Open Adventurer Plate Command";
    public override string Description => "Adds a command to open adventurer plates.";
    protected override string Command => "playerplate";
    protected override string HelpMessage => "Opens the character card for the selected character.";
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => ImGui.Text($"/{Command}");
    
    protected override void OnCommand(string arguments) {
        if (string.IsNullOrWhiteSpace(arguments)) {
            Service.Chat.PrintError($"/playerplate <t>");
            return;
        }
        var resolve = Framework.Instance()->GetUiModule()->GetPronounModule()->ResolvePlaceholder(arguments, 0, 0);
        if (resolve == null) {
            foreach (var actor in Service.Objects) {
                if (actor == null) continue;
                if (actor.Name.TextValue.Equals(arguments, StringComparison.InvariantCultureIgnoreCase)) {
                    resolve = (GameObject*)actor.Address;
                    break;
                }
            }
        }
        
        if (resolve != null && resolve->ObjectKind == 1 && resolve->SubKind == 4) {
            AgentCharaCard.Instance()->OpenCharaCard(resolve);
        } else {
            Service.Chat.PrintError($"{arguments} is not a player.");
        }
    }
}

