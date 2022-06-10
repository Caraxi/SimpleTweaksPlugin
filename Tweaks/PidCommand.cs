using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public class PidCommand : CommandTweak {
    public override string Name => "Show process id Command";
    public override string Description => $"Adds the command '/{Command}' to show current process id.";
    protected override string Command => "pid";
    protected override string HelpMessage => "Print your process id to chat.";

    protected override void OnCommand(string args) {
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
}