﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat; 

class ClickableLinks : ChatTweaks.SubTweak {
    public override string Name => "Clickable Links in Chat";
    public override string Description => "Parses links posted in chat and allows them to be clicked.";

    public override void Enable() {
        urlLinkPayload = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.OpenUrlLink, UrlLinkHandle);
        Service.Chat.ChatMessage += OnChatMessage;
        base.Enable();
    }

    private void UrlLinkHandle(uint id, SeString message) {
        var url = message.TextValue
            .Replace($"{(char) 0x00A0}", "");
        Common.OpenBrowser(url);
    }

    public override void Disable() {
        if (!Enabled) return;
        Service.Chat.ChatMessage -= OnChatMessage;
        PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.OpenUrlLink);
        base.Disable();
    }

    private readonly Regex urlRegex =
        new Regex(@"(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?",
            RegexOptions.Compiled);


    private DalamudLinkPayload urlLinkPayload;
    
    private static bool IsBattleType(XivChatType type) {
        var channel = ((int)type & 0x7F);
        switch (channel) {
            case 41: // Damage
            case 42: // Miss
            case 43: // Action
            case 44: // Item
            case 45: // Healing
            case 46: // GainBeneficialStatus
            case 48: // LoseBeneficialStatus
            case 47: // GainDetrimentalStatus
            case 49: // LoseDetrimentalStatus
            case 58: // BattleSystem
                return true;
            default:
                return false;
        }
    }

    private void OnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled) {
        if (IsBattleType(type)) {
            return;   
        }
        
        var isModified = false;
        var payloads = new List<Payload>();
        var cLinkDepth = 0;
            
        message.Payloads.ForEach(p => {
            // Don't create links inside other links.

            if (p is DalamudLinkPayload) {
                cLinkDepth++;
            } else if (cLinkDepth > 0 && p is RawPayload && RawPayload.LinkTerminator.Equals(p)) {
                cLinkDepth--;
            }

            if (cLinkDepth == 0 && p is TextPayload textPayload) {
                var match = urlRegex.Match(textPayload.Text);
                if (urlRegex.IsMatch(textPayload.Text)) {
                    var i = 0;
                    do {
                        if (match.Index > i) {
                            payloads.Add(new TextPayload(textPayload.Text.Substring(i, match.Index - i)));
                            i = match.Index;
                        }
                        payloads.Add(urlLinkPayload);
                        payloads.Add(new TextPayload($"{match.Value}"));
                        payloads.Add(RawPayload.LinkTerminator);
                        i += match.Value.Length;
                        match = match.NextMatch();
                    } while (match.Success);

                    if (i < textPayload.Text.Length) {
                        payloads.Add(new TextPayload(textPayload.Text.Substring(i)));
                    }
                    isModified = true;
                } else {
                    payloads.Add(p);
                }
            } else {
                payloads.Add(p);
            }
        });

        if (!isModified) return;
        message.Payloads.Clear();
        message.Payloads.AddRange(payloads);
    }
}
