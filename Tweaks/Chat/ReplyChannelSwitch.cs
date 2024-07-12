using System;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Reply Channel Switch")]
[TweakDescription("Allow typing /r to set active chat channel to Tell.")]
public class ReplyChannelSwitch : ChatTweaks.SubTweak {
    private string searchString = string.Empty;

    protected override void Enable() {
        searchString = Service.ClientState.ClientLanguage switch {
            ClientLanguage.Japanese => @"1番目に文字列の指定がありません。： /r",
            ClientLanguage.English => @"“/r” requires a valid string.",
            ClientLanguage.German => @"Das Textkommando „/r“ erfordert den Unterbefehl [Name/Eingabe] an 1. Stelle.",
            ClientLanguage.French => @"L'argument “nom” est manquant (/r).",
            _ => throw new ArgumentOutOfRangeException()
        };

        Service.Chat.CheckMessageHandled += CheckMesssage;
    }

    private void CheckMesssage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        if (message.TextValue != searchString) return;
        ChatHelper.SendMessage("/t <r>");
        isHandled = true;
    }

    protected override void Disable() => Service.Chat.CheckMessageHandled -= CheckMesssage;
}
