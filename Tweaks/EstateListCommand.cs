using System;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Estate List Command")]
[TweakDescription("Adds a command to open the estate list of one of your friends.")]
[Changelog("1.8.1.1", "Now allows partial matching of friend names.")]
[Changelog("1.8.7.2", "Fixed tweak not working in 6.4")]
public unsafe class EstateListCommand : CommandTweak {
    protected override string Command => "estatelist";
    protected override string HelpMessage => "Opens the estate list for one of your friends.";

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

        for (var i = 0U; i < agent->InfoProxy->EntryCount; i++) {
            var f = agent->InfoProxy->GetEntry(i);
            if (f == null) continue;
            if (f->HomeWorld != Service.ClientState.LocalPlayer?.CurrentWorld.RowId) continue;
            if (f->ContentId == 0) continue;
            if (f->Name[0] == 0) continue;
            if ((f->ExtraFlags & 32) != 0) continue;
            if (useContentId && contentId == f->ContentId) {
                AgentFriendlist.Instance()->OpenFriendEstateTeleportation(f->ContentId);
                return;
            }

            var name = f->NameString;
            if (name.StartsWith(arguments, StringComparison.InvariantCultureIgnoreCase)) {
                AgentFriendlist.Instance()->OpenFriendEstateTeleportation(f->ContentId);
                return;
            }
        }

        Service.Chat.PrintError($"No friend with name \"{arguments}\" on your current world.");
    }
}
