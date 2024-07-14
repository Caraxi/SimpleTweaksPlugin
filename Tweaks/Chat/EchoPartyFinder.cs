#nullable enable
using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Echo Party Finder")]
[TweakDescription("Prints Party Finder description to chat when joining a group and entering a duty.")]
[TweakAuthor("MidoriKami")]
[TweakVersion(2)]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.9.2.0", "Rewrote Tweak for Patch 6.5")]
public unsafe class EchoPartyFinder : ChatTweaks.SubTweak {
    public class Config : TweakConfig {
        [TweakConfigOption("Show PF description when joining party")]
        public bool ShowOnJoin = true;

        [TweakConfigOption("Show PF description when entering duty")]
        public bool ShowUponEnteringInstance = true;
    }
    
    public Config TweakConfig { get; private set; } = null!;

    [TweakHook]
    private HookWrapper<AgentLookingForGroup.Delegates.ReceiveEvent>? onLookingForGroupEventHook;

    private bool listenerActive;
    private string? partyDescription;
    private string? partyLeader;
    private uint? targetTerritoryId;

    protected override void Setup() {
        onLookingForGroupEventHook ??= Common.Hook<AgentLookingForGroup.Delegates.ReceiveEvent>(AgentModule.Instance()->GetAgentByInternalId(AgentId.LookingForGroup)->VirtualTable->ReceiveEvent, OnLookingForGroupReceiveEvent);
    }
    
    [TerritoryChanged]
    private void OnTerritoryChanged(ushort territoryId) {
        if (territoryId != targetTerritoryId) return;
        if (TweakConfig.ShowUponEnteringInstance) PrintListing();
    }
    
    private AtkValue* OnLookingForGroupReceiveEvent(AgentLookingForGroup* agent, AtkValue* rawData, AtkValue* args, uint argCount, ulong sender) {
        var result = onLookingForGroupEventHook!.Original(agent, rawData, args, argCount, sender);

        try {
            if (sender is 6 && argCount is 1 && args[0] is { Int: 0 }) {
                listenerActive = true;
            }
        } catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in EchoPartyFinder, let MidoriKami know!");
        }

        return result;
    }

    [AddonFinalize("LookingForGroupDetail")]
    private void OnAddonFinalize(AddonLookingForGroupDetail* addon) {
        if (!listenerActive) return;
        
        partyDescription = addon->DescriptionString.ToString();
        partyLeader = addon->PartyLeaderTextNode->NodeText.ToString();
        
        targetTerritoryId = Service.Data.GetExcelSheet<ContentFinderCondition>()!
            .FirstOrDefault(entry
                => string.Equals(
                    entry.Name.ToString(),
                    addon->DutyNameTextNode->NodeText.ToString(),
                    StringComparison.OrdinalIgnoreCase))?.TerritoryType.Row;
        
        if (TweakConfig.ShowOnJoin) PrintListing();
        listenerActive = false;
    }
    
    private void PrintListing() {
        if (partyDescription is not null && partyLeader is not null)
            Service.Chat.Print(new SeStringBuilder()
                .AddUiForeground("[Listing Info] ", 62)
                .AddUiForeground($"[{partyLeader}] ", 45)
                .AddText(partyDescription)
                .Build());
    }
}