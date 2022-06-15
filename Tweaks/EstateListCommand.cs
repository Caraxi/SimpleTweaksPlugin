using System;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class EstateListCommand : CommandTweak {
    public override string Name => "Estate List Command";
    public override string Description => $"Adds a command to open the estate list of one of your friends. (/{Command})";
    protected override string Command => "estatelist";
    protected override string HelpMessage => "Opens the estate list for one of your friends.";

    private delegate IntPtr ShowEstateTeleportationDelegate(AgentInterface* friendListAgent, ulong contentId);
    private ShowEstateTeleportationDelegate showEstateTeleportation;
    
    public override void Enable() {
        if (showEstateTeleportation == null && Service.SigScanner.TryScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 45 33 F6 48 8B CF 44 89 B3 ?? ?? ?? ?? E8", out var ptr)) {
            showEstateTeleportation = Marshal.GetDelegateForFunctionPointer<ShowEstateTeleportationDelegate>(ptr);
        }
        if (showEstateTeleportation == null) return;
        
        base.Enable();
    }

    protected override void OnCommand(string arguments) {
        if (string.IsNullOrWhiteSpace(arguments)) {
            Service.Chat.PrintError($"/{Command} <name>");
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
}
