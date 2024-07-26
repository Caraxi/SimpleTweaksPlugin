using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Show process id Command")]
[TweakDescription("Adds a command to show the current process id.")]
public class PidCommand : CommandTweak {
    protected override string Command => "pid";
    protected override string HelpMessage => "Print your process id to chat.";

    protected override void OnCommand(string args) {
        var process = Process.GetCurrentProcess();

        Service.Chat.Print(new XivChatEntry {
            Message = new SeString(new List<Payload> {
                new TextPayload("current pid: "),
                new UIForegroundPayload(22), 
                new TextPayload(process.Id.ToString()), 
                new UIForegroundPayload(0)
            })
        });
    }
}
