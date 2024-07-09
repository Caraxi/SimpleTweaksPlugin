using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakCategory(TweakCategory.Command)]
[TweakName("Show process id Command")]
[TweakDescription("Adds the command '/pid' to show current process id.")]
public class PidCommand : Tweak {

    private string command = "/pid";
    
    private unsafe void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        if (Common.LastCommand == null || Common.LastCommand->StringPtr == null) return;
        var lastCommandStr = Encoding.UTF8.GetString(Common.LastCommand->StringPtr, (int)Common.LastCommand->BufUsed);
        if (!lastCommandStr.Equals(command)){
            return;
        }

        isHandled = true;
        Service.Chat.Print(new XivChatEntry() {
            Message= new SeString(new List<Payload>() {
                new TextPayload("current pid: "),
                new UIForegroundPayload(22),
                new TextPayload(Process.GetCurrentProcess().Id.ToString()),
                new UIForegroundPayload(0)
            })
        });
    }

    protected override void Enable()
    {
        Service.Chat.ChatMessage += OnChatMessage;
    }
    protected override void Disable()
    {
        Service.Chat.ChatMessage -= OnChatMessage;
    }
    
}
