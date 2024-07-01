using System;
using System.Runtime.InteropServices;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class EstateListCommand : CommandTweak {
    public override string Name => "Estate List Command";
    public override string Description => $"Adds a command to open the estate list of one of your friends. (/{Command})";
    protected override string Command => "estatelist";
    protected override string HelpMessage => "Opens the estate list for one of your friends.";

    private delegate IntPtr ShowEstateTeleportationDelegate(AgentFriendlist* friendListAgent, ulong contentId);
    private ShowEstateTeleportationDelegate showEstateTeleportation;
    
    public override void Setup() {
        AddChangelog("1.8.1.1", "Now allows partial matching of friend names.");
        AddChangelog("1.8.7.2", "Fixed tweak not working in 6.4");
        base.Setup();
    }

    protected override void Enable() {
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
            var resolved = Framework.Instance()->GetUIModule()->GetPronounModule()->ResolvePlaceholder(arguments, 1, 0);
            if (resolved != null) {
                arguments = MemoryHelper.ReadStringNullTerminated(new IntPtr(resolved->GetName()));
            }
        }

        var useContentId = ulong.TryParse(arguments, out var contentId);

        var agent = AgentFriendlist.Instance();

        InfoProxyCommonList.CharacterData* friend = null;
        for (var i = 0U; i < agent->InfoProxy->GetEntryCount(); i++) {
            var f = agent->InfoProxy->GetEntry(i);
            if (f == null) continue;
            if (f->HomeWorld != Service.ClientState.LocalPlayer?.CurrentWorld.Id) continue;
            if (f->ContentId == 0) continue;
            if (f->Name[0] == 0) continue;
            if ((f->ExtraFlags & 32) != 0) continue;
            if (useContentId && contentId == (ulong)f->ContentId) {
                this.showEstateTeleportation(agent, (ulong)f->ContentId);
                return;
            }

            var name = f->NameString;
            if (name.StartsWith(arguments, StringComparison.InvariantCultureIgnoreCase)) {
                this.showEstateTeleportation(agent, (ulong)f->ContentId);
                return;
            }
        }
        
        Service.Chat.PrintError($"No friend with name \"{arguments}\" on your current world.");
    }
}
