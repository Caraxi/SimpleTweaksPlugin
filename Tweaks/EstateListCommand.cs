﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class EstateListCommand : CommandTweak {
    public override string Name => "Estate List Command";
    public override string Description => $"Adds a command to open the estate list of one of your friends. (/{Command})";
    protected override string Command => "estatelist";
    protected override string HelpMessage => "Opens the estate list for one of your friends.";

    private delegate IntPtr ShowEstateTeleportationDelegate(AgentInterface* friendListAgent, ulong contentId);
    private ShowEstateTeleportationDelegate showEstateTeleportation;
    
    public override void Setup() {
        AddChangelog("1.8.1.1", "Now allows partial matching of friend names.");
        base.Setup();
    }

    public override void Enable() {
        if (showEstateTeleportation == null && Service.SigScanner.TryScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 33 ED 48 8B CF 89 AB ?? ?? ?? ?? E8", out var ptr)) {
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

        if (arguments.StartsWith("<") && arguments.EndsWith(">")) {
            var resolved = Framework.Instance()->GetUiModule()->GetPronounModule()->ResolvePlaceholder(arguments, 1, 0);
            if (resolved != null) {
                arguments = MemoryHelper.ReadStringNullTerminated(new IntPtr(resolved->GetName()));
            }
        }

        var useContentId = ulong.TryParse(arguments, out var contentId);
        var friends = FriendList.List
            .Where(friend => {
                if (friend.HomeWorld != Service.ClientState.LocalPlayer?.CurrentWorld.Id) return false;
                if (useContentId && contentId > 0 && friend.ContentId == contentId) return true;
                return friend.Name.TextValue.StartsWith(arguments, StringComparison.InvariantCultureIgnoreCase);
            }).ToList();
        
        var friend = friends.FirstOrDefault(friend => friend.Name.TextValue.StartsWith(arguments, StringComparison.InvariantCultureIgnoreCase));
        if (friend.ContentId == 0) friend = friends.FirstOrDefault();
        if (friend.ContentId == 0) {
            Service.Chat.PrintError($"No friend with name \"{arguments}\" on your current world.");
            return;
        }
        
        var friendListAgent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialFriendList);
        if (friendListAgent != null) this.showEstateTeleportation(friendListAgent, friend.ContentId);
    }
}
