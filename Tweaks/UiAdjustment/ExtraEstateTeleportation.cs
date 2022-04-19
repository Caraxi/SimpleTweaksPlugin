#nullable enable
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui.ContextMenus;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using XivCommon.Functions.FriendList;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

internal class ExtraEstateTeleportation : UiAdjustments.SubTweak {
    private static class Signatures {
        internal const string ShowEstateTeleportation = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 45 33 F6 48 8B CF 44 89 B3 ?? ?? ?? ?? E8";
    }

    public override string Name => "Extra Estate Teleportation";
    public override string Description => "Add the Estate Teleportation option to more locations.";
    protected override string Author => "Anna";

    private Configs Config { get; set; } = null!;

    private delegate IntPtr ShowEstateTeleportationDelegate(IntPtr friendListAgent, ulong contentId);

    private ShowEstateTeleportationDelegate? ShowEstateTeleportation { get; set; }

    public class Configs : TweakConfig {
        public bool PartyMemberList = true;
        public bool PartyList;
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox(LocString("PartyMemberList", "Party Member List (Social window)"), ref this.Config.PartyMemberList);
        hasChanged |= ImGui.Checkbox(LocString("PartyList", "Party List"), ref this.Config.PartyList);
    };

    public override void Enable() {
        if (this.ShowEstateTeleportation == null && Service.SigScanner.TryScanText(Signatures.ShowEstateTeleportation, out var ptr)) {
            this.ShowEstateTeleportation = Marshal.GetDelegateForFunctionPointer<ShowEstateTeleportationDelegate>(ptr);
        }

        this.Config = this.LoadConfig<Configs>() ?? new Configs();
        Service.ContextMenu.ContextMenuOpened += this.OnContextMenu;
        base.Enable();
    }

    public override void Disable() {
        Service.ContextMenu.ContextMenuOpened -= this.OnContextMenu;
        this.SaveConfig(this.Config);
        base.Disable();
    }

    private void OnContextMenu(ContextMenuOpenedArgs args) {
        SimpleLog.Log("Open Context Menu");
        if (this.ShowEstateTeleportation == null || args.ParentAddonName is not ("PartyMemberList" or "_PartyList")) {
            return;
        }

        switch (args.ParentAddonName) {
            case "PartyMemberList" when !this.Config.PartyMemberList:
            case "_PartyList" when !this.Config.PartyList:
                return;
        }

        var friends = this.Plugin.XivCommon.Functions.FriendList.List
            .Where(friend => friend.ContentId == args.GameObjectContext?.ContentId && friend.HomeWorld == Service.ClientState.LocalPlayer?.CurrentWorld.Id)
            .ToArray();

        FriendListEntry? friend = null;
        if (friends.Length == 1) {
            SimpleLog.Log("Single Friend Match");
            friend = friends[0];
        } else {
            SimpleLog.Log("Multiple Friends Match");
            var matched = friends.Where(friend => friend.Name.TextValue == args.Title).ToArray();
            if (matched.Length > 0) {
                friend = matched[0];
            }
        }

        if (friend == null) {
            SimpleLog.Log("Failed to find Friend");
            return;
        }
        
        var estateTeleportationString = Service.Data.Excel.GetSheet<Addon>()?.GetRow(6865)?.Text.RawString;
        args.AddCustomItem(estateTeleportationString ?? "Estate Teleportation", _ => {
            if (this.ShowEstateTeleportation == null) {
                return;
            }

            var friendListAgent = this.Plugin.XivCommon.Functions.GetAgentByInternalId(54);
            if (friendListAgent != IntPtr.Zero) {
                this.ShowEstateTeleportation(friendListAgent, friend.Value.ContentId);
            }
        });
    }
}