#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe partial class EchoPartyFinder : ChatTweaks.SubTweak
{
    public override string Name => "Echo Party Finder";
    public override string Description => "Prints Party Finder description to chat upon joining a group.";
    protected override string Author => "MidoriKami";

    private AgentInterface* AgentLookingForGroup => AgentModule.Instance()->GetAgentByInternalId(AgentId.LookingForGroup);
    private delegate nint ReceiveEventDelegate(AgentInterface* agent, nint rawData, AtkValue* args, uint argCount, ulong sender);
    private HookWrapper<ReceiveEventDelegate>? onLookingForGroupEventHook;
    
    private static AddonLookingForGroupDetail* Addon => (AddonLookingForGroupDetail*) Common.GetUnitBase("LookingForGroupDetail");

    private SeString? message;
    private bool messageShown;
    private uint territoryType;
    
    [GeneratedRegex("[^\\p{L}\\p{N}]")]
    public static partial Regex Alphanumeric();
    
    public class Config : TweakConfig
    {
        [TweakConfigOption("Show PF description when joining party")]
        public bool ShowOnJoin = true;

        [TweakConfigOption("Show PF description when entering duty")]
        public bool ShowUponEnteringInstance = true;
    }

    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak(Changelog.UnreleasedVersion).Author("MidoriKami");

        onLookingForGroupEventHook ??= Common.Hook(AgentLookingForGroup->VTable->ReceiveEvent, new ReceiveEventDelegate(OnLookingForGroupReceiveEvent));
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();

        onLookingForGroupEventHook?.Enable();
        Service.ClientState.TerritoryChanged += OnZoneChange;
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);

        onLookingForGroupEventHook?.Disable();
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        base.Disable();
    }

    public override void Dispose()
    {
        onLookingForGroupEventHook?.Dispose();
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        base.Dispose();
    }

    private void OnZoneChange(object? sender, ushort e)
    {
        if (!TweakConfig.ShowUponEnteringInstance) return;
        if (message is null) return;
        if (messageShown) return;
        if (e != territoryType) return;

        Service.Chat.Print(message);
        messageShown = true;
    }
    
    private nint OnLookingForGroupReceiveEvent(AgentInterface* agent, nint rawData, AtkValue* args, uint argCount, ulong sender)
    {
        var result = onLookingForGroupEventHook!.Original(agent, rawData, args, argCount, sender);

        try
        {
            if (sender is 6 && argCount is 1 && args[0].Int is 0)
            {
                message = new SeStringBuilder()
                    .AddUiForeground($"[Listing Info] ", 62)
                    .AddText(Addon->DescriptionString.ToString())
                    .Build();

                territoryType = GetTerritoryType();
                
                messageShown = false;
                
                if(TweakConfig.ShowOnJoin) Service.Chat.Print(message);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in EchoPartyFinder, let MidoriKami know!");
        }

        return result;
    }
    
    private uint GetTerritoryType()
    {
        var dutyName = Alphanumeric().Replace(Addon->DutyNameTextNode->NodeText.ToString(), string.Empty);

        foreach (var cfc in Service.Data.GetExcelSheet<ContentFinderCondition>()!)
        {
            var cfcName = Alphanumeric().Replace(cfc.Name, string.Empty);

            if (cfcName == dutyName)
            {
                return cfc.TerritoryType.Row;
            }
        }
        
        return 0;
    }
    
    //
    // Temporary until ClientStructs Merges this struct
    //
    [StructLayout(LayoutKind.Explicit, Size = 0x3E8)]
    public struct AddonLookingForGroupDetail
    {
        [FieldOffset(0x00)] public AtkUnitBase AtkUnitBase;

        [FieldOffset(0x248)] public AtkComponentButton* JoinPartyButton;

        // [FixedSizeArray<Pointer<AtkComponentButton>>(6)]
        // [FieldOffset(0x250)] public fixed byte JoinAllianceButtons[0x8 * 6];

        [FieldOffset(0x280)] public AtkComponentButton* SendTellButton;
        [FieldOffset(0x288)] public AtkComponentButton* AllianceBackButton; // Not visible in 8-man parties
        [FieldOffset(0x290)] public AtkComponentButton* BackButton;

        [FieldOffset(0x2C8)] public AtkImageNode* LookingForGroupImageNode;
        [FieldOffset(0x2D0)] public AtkTextNode* PartyLeaderTextNode;
        [FieldOffset(0x2E0)] public AtkTextNode* TimeRemainingTextNode;
        [FieldOffset(0x2E8)] public AtkTextNode* DutyNameTextNode;
        [FieldOffset(0x2D8)] public AtkTextNode* LocationTextNode;
        [FieldOffset(0x2F0)] public AtkTextNode* ItemLevelTextNode;
        [FieldOffset(0x2F0)] public AtkTextNode* StatusTextNode;
        [FieldOffset(0x300)] public AtkTextNode* DescriptionTextNode;
    
        [FieldOffset(0x308)] public Utf8String DescriptionString;
        [FieldOffset(0x370)] public Utf8String CategoriesString; // Duty Complete, Loot, One Player Per Job
    }
}