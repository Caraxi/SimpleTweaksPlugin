#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Fade Unavailable Actions")]
[TweakDescription("Instead of darkening icons, makes them transparent when unavailable.")]
[TweakAuthor("MidoriKami")]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.3.1")]
[TweakVersion(2)]
[Changelog("1.8.3.2", "Tweak now only applies to the icon image itself and not the entire button")]
[Changelog("1.8.3.2", "Add option to apply transparency to the slot frame of the icon")]
[Changelog("1.8.3.2", "Add option to apply to sync'd skills only")]
[Changelog("1.8.4.0", "Tweak now only applies to combat actions")]
[Changelog("1.8.4.0", "Properly resets hotbar state on unload/disable")]
[Changelog("1.9.2.0", "Added option to make skills that are out of range red")]
public unsafe class FadeUnavailableActions : UiAdjustments.SubTweak {
    private delegate void UpdateHotBarSlotDelegate(AddonActionBarBase* addon, ActionBarSlot* uiData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex);
    
    [TweakHook, Signature("E8 ?? ?? ?? ?? 49 81 C7 ?? ?? ?? ?? 83 C7 11", DetourName = nameof(OnHotBarSlotUpdate))]
    private readonly HookWrapper<UpdateHotBarSlotDelegate>? onHotBarSlotUpdateHook;

    private readonly Dictionary<uint, Action?> actionCache = [];

    private readonly List<string> addonActionBarNames = [
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
        "_ActionCross",
        "_ActionDoubleCrossR",
        "_ActionDoubleCrossL",
    ];
    
    public class Config : TweakConfig {
        [TweakConfigOption("Fade Percentage", IntMax = 90, IntMin = 0, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int FadePercentage = 70;

        [TweakConfigOption("Apply Transparency to Frame")]
        public bool ApplyToFrame = true;

        [TweakConfigOption("Redden Percentage", IntMax = 100, IntMin = 5, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int ReddenPercentage = 50;
        
        [TweakConfigOption("Redden skills out of range")]
        public bool ReddenOutOfRange = true;

        [TweakConfigOption("Apply Only to Sync'd Actions")]
        public bool ApplyToSyncActions;
    }
    
    public Config TweakConfig { get; private set; } = null!;

    protected override void Disable() {
        ResetAllHotBars();
    }
    
    private void ResetAllHotBars() {
        foreach (var addonName in addonActionBarNames) {
            var addon = Common.GetUnitBase<AddonActionBarBase>(addonName);
            if (addon is null) continue;

            foreach (var slot in addon->ActionBarSlotVector) {
                if (slot.Icon is not null) {
                    var iconComponent = (AtkComponentIcon*) slot.Icon->Component;
                    if (iconComponent is not null) {
                        iconComponent->IconImage->Color.A = 0xFF;
                        iconComponent->IconImage->Color.R = 0xFF;
                        iconComponent->IconImage->Color.G = 0xFF;
                        iconComponent->IconImage->Color.B = 0xFF;
                        iconComponent->Frame->Color.A = 0xFF;
                    }
                }
            }
        }
    }
    
    private void OnHotBarSlotUpdate(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex) {
        try {
            ProcessHotBarSlot(hotBarSlotData, numberArray, numberArrayIndex);
        } 
        catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in FadeUnavailableActions, let MidoriKami know!");
        }
        
        onHotBarSlotUpdateHook!.Original(addon, hotBarSlotData, numberArray, stringArray, numberArrayIndex, stringArrayIndex);
    }

    private void ProcessHotBarSlot(ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, int numberArrayIndex) {
        if (Service.ClientState.LocalPlayer is { IsCasting: true } ) return;

        var numberArrayData = (NumberArrayStruct*) (&numberArray->IntArray[numberArrayIndex]);

        if (numberArrayData->ActionType is not (NumberArrayActionType.Action or NumberArrayActionType.CraftAction)) {
            ApplyTransparency(hotBarSlotData, false);
            return;
        }

        if (TweakConfig.ApplyToSyncActions) {
            var action = GetAction(numberArrayData->ActionId);
            if (action == null) { 
                ApplyTransparency(hotBarSlotData, false);
                ApplyReddening(hotBarSlotData, false);
                return;
            }
            var actionLevel = action.ClassJobLevel;
            var playerLevel = Service.ClientState.LocalPlayer?.Level ?? 0;

            switch (action) {
                case { IsRoleAction: false } when actionLevel > playerLevel:
                    ApplyTransparency(hotBarSlotData, true);
                    ApplyReddening(hotBarSlotData, false);
                    break;
                
                default:
                    ApplyTransparency(hotBarSlotData, false);
                    ApplyReddening(hotBarSlotData, false);
                    break;
            }
        }
        else {
            ApplyTransparency(hotBarSlotData, ShouldFadeAction(numberArrayData));
            ApplyReddening(hotBarSlotData, !numberArrayData->TargetInRange && !ShouldFadeAction(numberArrayData));
        }
    }

    private Action? GetAction(uint actionId) {
        var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);

        if (actionCache.TryGetValue(adjustedActionId, out var action)) return action;

        action = Service.Data.GetExcelSheet<Action>()!.GetRow(adjustedActionId);
        actionCache.Add(adjustedActionId, action);
        return action;
    }

    private bool ShouldFadeAction(NumberArrayStruct* numberArrayData) 
        => !(numberArrayData->ActionAvailable_1 && numberArrayData->ActionAvailable_2);
    
    private void ApplyReddening(ActionBarSlot* hotBarSlotData, bool redden) {
        if (hotBarSlotData is null) return;
        var iconComponent = (AtkComponentIcon*) hotBarSlotData->Icon->Component;

        if (iconComponent is null) return;
        if (iconComponent->IconImage is null) return;
        if (iconComponent->Frame is null) return;

        if (TweakConfig.ReddenOutOfRange && redden) {
            iconComponent->IconImage->Color.R = 0xFF;
            iconComponent->IconImage->Color.G = (byte)(0xFF * ((100 - TweakConfig.ReddenPercentage) / 100.0f));
            iconComponent->IconImage->Color.B = (byte)(0xFF * ((100 - TweakConfig.ReddenPercentage) / 100.0f));
        }
        else {
            iconComponent->IconImage->Color.R = 0xFF;
            iconComponent->IconImage->Color.G = 0xFF;
            iconComponent->IconImage->Color.B = 0xFF;
        }
    }
    
    private void ApplyTransparency(ActionBarSlot* hotBarSlotData, bool fade) {
        if (hotBarSlotData is null) return;
        var iconComponent = (AtkComponentIcon*) hotBarSlotData->Icon->Component;

        if (iconComponent is null) return;
        if (iconComponent->IconImage is null) return;
        if (iconComponent->Frame is null) return;

        if (fade) {
            iconComponent->IconImage->Color.A = (byte)(0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
            if(TweakConfig.ApplyToFrame) iconComponent->Frame->Color.A = (byte)(0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
        }
        else {
            iconComponent->IconImage->Color.A = 0xFF;
            iconComponent->Frame->Color.A = 0xFF;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    private struct NumberArrayStruct {
        [FieldOffset(0x00)] public NumberArrayActionType ActionType;
        [FieldOffset(0x0C)] public uint ActionId;
        [FieldOffset(0x14)] public bool ActionAvailable_1;
        [FieldOffset(0x18)] public bool ActionAvailable_2;
        [FieldOffset(0x20)] public int CooldownPercent;
        [FieldOffset(0x28)] public int ManaCost;
        [FieldOffset(0x40)] public bool TargetInRange;
    }

    private enum NumberArrayActionType: uint {
        Action = 0x2E,
        CraftAction = 0x36,
        
        // Note, 7.0 added new a new type before 0x2E, and potentially more after, unused values were removed.
    }
}