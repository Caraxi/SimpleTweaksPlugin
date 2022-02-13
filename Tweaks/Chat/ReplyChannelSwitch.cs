using System;
using Dalamud;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace SimpleTweaksPlugin.Tweaks.Chat; 

public class ReplyChannelSwitch : ChatTweaks.SubTweak {
    public override string Name => "Reply Channel Switch";
    public override string Description => "Allow typing /r to set active chat channel to Tell.";

    private string searchString = string.Empty; 

    public override void Enable() {
        searchString = Service.ClientState.ClientLanguage switch {
            ClientLanguage.Japanese => @"1番目に文字列の指定がありません。： /r",
            ClientLanguage.English => @"“/r” requires a valid string.",
            ClientLanguage.German => @"Das Textkommando „/r“ erfordert den Unterbefehl [Name/Eingabe] an 1. Stelle.",
            ClientLanguage.French => @"L'argument “nom” est manquant (/r).",
            _ => throw new ArgumentOutOfRangeException()
        };

        Service.Chat.CheckMessageHandled += CheckMesssage;
        base.Enable();
    }

    private void CheckMesssage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        if (message.TextValue == searchString) {
            Plugin.XivCommon.Functions.Chat.SendMessage("/t <r>");
            isHandled = true;
        }
    }

    public override void Disable() {
        Service.Chat.CheckMessageHandled -= CheckMesssage;
        base.Disable();
    }
}