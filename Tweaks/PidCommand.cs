using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Command;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class PidCommand : Tweak {
    public override string Name => "Show process id Command";
    public override string Description => "Adds the command '/pid' to show current process id.";

    public override void Setup() {
        Ready = true;
    }

    public override void Enable() {
        if (Enabled) return;
        Service.Commands.AddHandler("/pid", new CommandInfo(CommandHandler) {ShowInHelp = true, HelpMessage = "Print your process id to chat."});

        Enabled = true;
    }
        
    private void CommandHandler(string command, string arguments) {

        var process = Process.GetCurrentProcess();
            
        Service.Chat.PrintChat(new XivChatEntry() {
            Message= new SeString(new List<Payload>() {
                new TextPayload("current pid: "),
                new UIForegroundPayload(22),
                new TextPayload(process.Id.ToString()),
                new UIForegroundPayload(0)
            })
        });
    }
        
    public override void Disable() {
        Service.Commands.RemoveHandler("/pid");
        Enabled = false;
    }

    public override void Dispose() {
        Enabled = false;
        Ready = false;
    }
}