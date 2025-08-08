using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakName("Character Directory Command")]
[TweakDescription("Adds a command to open the directory where client side character data is stored.")]
public class ChrDirCommand : CommandTweak {
    protected override string Command => "chrdir";
    protected override string HelpMessage => "Print your character save directory to chat. '/chrdir open' to open the directory in explorer.";

    [LinkHandler(LinkHandlerId.OpenFolderLink, nameof(OpenFolder))]
    private DalamudLinkPayload linkPayload;

    private void OpenFolder(SeString arg2) {
        var dir = arg2.TextValue.Replace($"{(char)0x00A0}", "").Replace("\n", "").Replace("\r", "");
        Process.Start("explorer.exe", dir);
    }

    protected override unsafe void OnCommand(string arguments) {
        var saveDir = Path.Combine(Framework.Instance()->UserPathString, $"FFXIV_CHR{Service.ClientState.LocalContentId:X16}").Replace('/', '\\');
        if (arguments == "open") {
            Process.Start("explorer.exe", saveDir);
            return;
        }

        Service.Chat.Print(new XivChatEntry() {
            Message= new SeString(new List<Payload>() {
                new TextPayload("Character Directory:\n"),
                new UIForegroundPayload(22),
                linkPayload,
                new TextPayload(saveDir),
                RawPayload.LinkTerminator,
                new UIForegroundPayload(0)
            })
        });
    }

    protected void DrawConfig() {
        ImGui.TextDisabled($"{CustomOrDefaultCommand}");
        ImGui.TextDisabled($"{CustomOrDefaultCommand} open");
    }

    protected override void DisableCommand() {
        Service.Chat.RemoveChatLinkHandler(linkPayload.CommandId);
    }
}
