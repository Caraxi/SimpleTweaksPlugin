using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
namespace SimpleTweaksPlugin.Tweaks; 

public class ChrDirCommand : CommandTweak {
    public override string Name => "Character Directory Command";
    public override string Description => "Adds a command to open the directory when client side character data is stored.";
    protected override string Command => "chrdir";
    protected override string HelpMessage => "Print your character save directory to chat. '/chrdir open' to open the directory in explorer.";

    private DalamudLinkPayload linkPayload;

    public override void Enable() {
        if (Enabled) return;
        linkPayload = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.OpenFolderLink, OpenFolder);
        base.Enable();
    }

    private void OpenFolder(uint arg1, SeString arg2) {
        var dir = arg2.TextValue.Replace($"{(char)0x00A0}", "").Replace("\n", "").Replace("\r", "");
        Process.Start("explorer.exe", dir);
    }

    protected override unsafe void OnCommand(string arguments) {
        var saveDir = Path.Combine(Framework.Instance()->UserPath, $"FFXIV_CHR{Service.ClientState.LocalContentId:X16}");
        if (arguments == "open") {
            Process.Start("explorer.exe", saveDir);
            return;
        }

        Service.Chat.PrintChat(new XivChatEntry() {
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

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) => {
        ImGui.TextDisabled($"/{Command}");
        ImGui.TextDisabled($"/{Command} open");
    };

    public override void Disable() {
        PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.OpenFolderLink);
        base.Disable();
    }
}