using System;
using Dalamud.Game.Command;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class CharaCardCommand : Tweak {
    public override string Name => "Open Adventurer Plate Command";
    public override string Description => "Adds a command to open adventurer plates.";
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => ImGui.Text("/playerplate");
    
    public override void Enable() {
        Service.Commands.AddHandler("/playerplate", new CommandInfo(CharaCardCommandHandler) {
            HelpMessage = "Opens the character card for the selected character.",
            ShowInHelp = true,
        });
        base.Enable();
    }
    
    private void CharaCardCommandHandler(string command, string arguments) {
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
            var name = MemoryHelper.ReadStringNullTerminated(new IntPtr(resolve->GetName()));
            AgentCharaCard.Instance()->OpenCharaCard(resolve);
        } else {
            Service.Chat.PrintError($"{arguments} is not a player.");
        }
    }

    public override void Disable() {
        Service.Commands.RemoveHandler("/playerplate");
        base.Disable();
    }
}

