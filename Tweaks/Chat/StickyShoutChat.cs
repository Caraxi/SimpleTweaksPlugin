using System.Linq;
using Dalamud;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.Log;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Sticky Shout Chat")]
[TweakDescription("Prevents the game from automatically switching out of shout chat.")]
[TweakReleaseVersion("1.9.6.0")]
public class StickyShoutChat : ChatTweaks.SubTweak {
    private nint editAddress;

    protected override void Enable() {
        Service.Chat.CheckMessageHandled -= ChatOnCheckMessageHandled;
        Service.Chat.CheckMessageHandled += ChatOnCheckMessageHandled;
        
        if (!Service.SigScanner.TryScanText("05 75 0C 8B D3 E8 ?? ?? ?? ?? E9", out editAddress)) return;
        SafeMemory.Write(editAddress, (sbyte)-2);
    }


    private readonly string[] errorMessages = [
        "“/shout” requires a valid string.",
        "“/shout ” requires a valid string.",
    ];
    
    private unsafe void ChatOnCheckMessageHandled(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type != XivChatType.ErrorMessage) return;
        var text = message.TextValue;
        if (!errorMessages.Any(m => m.Equals(text))) return;
        RaptureShellModule.Instance()->ChangeChatChannel(5, 0, Utf8String.FromString(""), true);
        isHandled = true;
    }

    protected override void Disable() {
        Service.Chat.CheckMessageHandled -= ChatOnCheckMessageHandled;
        if (editAddress == nint.Zero) return;
        if (SafeMemory.Write(editAddress, (sbyte)5))
            editAddress = nint.Zero;
    }
}
