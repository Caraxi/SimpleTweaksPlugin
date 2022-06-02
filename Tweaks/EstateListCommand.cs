using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class EstateListCommand : Tweak {
    public override string Name => "Estate List Command";
    public override string Description => "Adds a command to open the estate list of one of your friends. (/estatelist)";

    private delegate IntPtr ShowEstateTeleportationDelegate(AgentInterface* friendListAgent, ulong contentId);
    private ShowEstateTeleportationDelegate showEstateTeleportation;
    
    public override void Enable() {
        if (showEstateTeleportation == null && Service.SigScanner.TryScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 45 33 F6 48 8B CF 44 89 B3 ?? ?? ?? ?? E8", out var ptr)) {
            showEstateTeleportation = Marshal.GetDelegateForFunctionPointer<ShowEstateTeleportationDelegate>(ptr);
        }
        if (showEstateTeleportation == null) return;

        Service.Commands.AddHandler("/estatelist", new CommandInfo(EstateListCommandHandle) {
            ShowInHelp = true,
            HelpMessage = "Opens the estate list for one of your friends."
        });

        base.Enable();
    }

    private void EstateListCommandHandle(string command, string arguments) {
        if (string.IsNullOrWhiteSpace(arguments)) {
            Service.Chat.PrintError("/estatelist <name>");
            return;
        }
        var useContentId = ulong.TryParse(arguments, out var contentId);
        var friend = Plugin.XivCommon.Functions.FriendList.List
            .FirstOrDefault(friend => {
                if (friend.HomeWorld != Service.ClientState.LocalPlayer?.CurrentWorld.Id) return false;
                if (useContentId && contentId > 0 && friend.ContentId == contentId) return true;
                return friend.Name.TextValue.Equals(arguments, StringComparison.InvariantCultureIgnoreCase);
            });
        
        if (friend.ContentId == 0) {
            Service.Chat.PrintError($"No friend with name \"{arguments}\" on your current world.");
            return;
        }
        
        var friendListAgent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialFriendList);
        if (friendListAgent != null) this.showEstateTeleportation(friendListAgent, friend.ContentId);
    }

    public override void Disable() {
        Service.Commands.RemoveHandler("/estatelist");
        base.Disable();
    }
}
