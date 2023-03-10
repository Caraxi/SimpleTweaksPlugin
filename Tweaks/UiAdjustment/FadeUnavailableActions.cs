#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class FadeUnavailableActions : UiAdjustments.SubTweak
{
    public override string Name => "Fade Unavailable Actions";
    public override string Description => "Instead of darkening icons, makes them transparent when unavailable";
    protected override string Author => "MidoriKami";

    private delegate void UpdateHotBarSlotDelegate(AddonActionBarBase* addon, SlotData* uiData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex);
    
    [Signature("E8 ?? ?? ?? ?? 49 81 C6 ?? ?? ?? ?? 83 C7 10", DetourName = nameof(OnHotBarSlotUpdate))]
    private readonly Hook<UpdateHotBarSlotDelegate>? onHotBarSlotUpdateHook = null!;

    private readonly Dictionary<uint, Action> actionCache = new();

    public class Config : TweakConfig
    {
        [TweakConfigOption("Fade Percentage", IntMax = 90, IntMin = 0, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int FadePercentage = 70;

        [TweakConfigOption("Apply Transparency to Frame")]
        public bool ApplyToFrame = true;

        [TweakConfigOption("Apply Only to Sync'd Actions")]
        public bool ApplyToSyncActions = false;
    }
    
    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.3.1");
        AddChangelog("1.8.3.2", "Tweak now only applies to the icon image itself and not the entire button");
        AddChangelog("1.8.3.2", "Add option to apply transparency to the slot frame of the icon");
        AddChangelog("1.8.3.2", "Add option to apply to sync'd skills only");
        AddChangelog(Changelog.UnreleasedVersion, "Tweak now only applies to combat actions");
        
        SignatureHelper.Initialise(this);
        
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        onHotBarSlotUpdateHook?.Enable();
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        
        onHotBarSlotUpdateHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        onHotBarSlotUpdateHook?.Dispose();
        base.Dispose();
    }

    private void OnHotBarSlotUpdate(AddonActionBarBase* addon, SlotData* hotBarSlotData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex)
    {
        try
        {
            ProcessHotBarSlot(hotBarSlotData, numberArray, numberArrayIndex);
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in FadeUnavailableActions, let MidoriKami know!");
        }
        
        onHotBarSlotUpdateHook!.Original(addon, hotBarSlotData, numberArray, stringArray, numberArrayIndex, stringArrayIndex);
    }

    private void ProcessHotBarSlot(SlotData* hotBarSlotData, NumberArrayData* numberArray, int numberArrayIndex)
    {
        if (hotBarSlotData->ActionId > ushort.MaxValue) return;
        if (Service.ClientState.LocalPlayer is { IsCasting: true } ) return;

        var numberArrayData = (NumberArrayStruct*) (&numberArray->IntArray[numberArrayIndex]);
        
        // If action type is not combat action, remove transparency and return
        if (numberArrayData->ActionType != 45)
        {
            ApplyTransparency(hotBarSlotData, false);
            return;
        }

        if (TweakConfig.ApplyToSyncActions)
        {
            var action = GetAction(hotBarSlotData->ActionId);
            var actionLevel = action.ClassJobLevel;
            var playerLevel = Service.ClientState.LocalPlayer?.Level ?? 0;

            switch (action)
            {
                case { IsRoleAction: false } when actionLevel > playerLevel:
                    ApplyTransparency(hotBarSlotData, true);
                    break;
                
                default:
                    ApplyTransparency(hotBarSlotData, false);
                    break;
            }
        }
        else
        {
            ApplyTransparency(hotBarSlotData, ShouldFadeAction(numberArrayData));
        }
    }

    private Action GetAction(uint actionId)
    {
        var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);

        if (actionCache.TryGetValue(adjustedActionId, out var action)) return action;

        action = Service.Data.GetExcelSheet<Action>()!.GetRow(adjustedActionId)!;
        actionCache.Add(adjustedActionId, action);
        return action;
    }

    private bool ShouldFadeAction(NumberArrayStruct* numberArrayData) => !(numberArrayData->ActionAvailable_1 && numberArrayData->ActionAvailable_2);

    private void ApplyTransparency(SlotData* hotBarSlotData, bool fade)
    {
        if (hotBarSlotData is null) return;
        var iconComponent = (AtkComponentIcon*) hotBarSlotData->IconComponentNode->Component;

        if (iconComponent is null) return;
        if (iconComponent->IconImage is null) return;
        if (iconComponent->Frame is null) return;

        if (fade)
        {
            iconComponent->IconImage->AtkResNode.Color.A = (byte)(0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
            if(TweakConfig.ApplyToFrame) iconComponent->Frame->Color.A = (byte)(0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
        }
        else
        {
            iconComponent->IconImage->AtkResNode.Color.A = 0xFF;
            iconComponent->Frame->Color.A = 0xFF;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    private struct NumberArrayStruct
    {
        [FieldOffset(0x00)] public uint ActionType;
        [FieldOffset(0x0C)] public uint ActionId;
        [FieldOffset(0x14)] public bool ActionAvailable_1;
        [FieldOffset(0x18)] public bool ActionAvailable_2;
        [FieldOffset(0x20)] public int CooldownPercent;
        [FieldOffset(0x28)] public int ManaCost;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 0xC8)]
    private struct SlotData
    {
        [FieldOffset(0x04)] public uint ActionId;
        [FieldOffset(0x88)] public AtkComponentDragDrop* DragDropNode;
        [FieldOffset(0x90)] public AtkComponentNode* IconComponentNode;
        [FieldOffset(0x98)] public AtkTextNode* HotkeyTextNode;
        [FieldOffset(0xA0)] public AtkResNode* FrameNode;
        [FieldOffset(0xA8)] public AtkImageNode* ChargeIconNode;
        [FieldOffset(0xB0)] public AtkResNode* RecastNode;
        [FieldOffset(0xB8)] public byte* TooltipString;
    }
}