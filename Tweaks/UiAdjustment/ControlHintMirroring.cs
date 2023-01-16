﻿using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ControlHintMirroring : UiAdjustments.SubTweak
{

    struct HotbarSlotCommand
    {
        public uint Id;
        public HotbarSlotType Type;
    }

    private delegate byte ActionBarBaseUpdate(AddonActionBarBase* addonActionBarBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);

    private HookWrapper<ActionBarBaseUpdate> actionBarBaseUpdateHook;

    private ActionManager* actionManager;

    public override string Name => "Duplicate Keybind Hints Between Hotbars";
    public override string Description => "Will display the keybind hint for any hotbar slot onto unbound slots with the same action.";
    protected override string Author => "BoredDan";

    private static readonly string[] allActionBars = {
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
    };

    private const int StrDataIndex = 5;
    private const int StrDataHotbarLength = 16;
    private const int StrDataSlotLength = 3;
    private const int StrDataHintIndex = 2;

    private static readonly int actionBarLength = 12;
    private string[][] recordedControlHints = new string[allActionBars.Length][];
    private HotbarSlotCommand[][] recordedCommands = new HotbarSlotCommand[allActionBars.Length][];

    public override void Enable()
    {
        UpdateAll();
        actionBarBaseUpdateHook ??= Common.Hook<ActionBarBaseUpdate>("E8 ?? ?? ?? ?? 83 BB ?? ?? ?? ?? ?? 75 09", ActionBarBaseUpdateDetour);
        actionBarBaseUpdateHook?.Enable();
        actionManager = ActionManager.Instance();
        base.Enable();
    }

    private static AddonActionBarBase* GetActionBarAddon(string actionBar)
    {
        return (AddonActionBarBase*)Service.GameGui.GetAddonByName(actionBar, 1);
    }

    private byte ActionBarBaseUpdateDetour(AddonActionBarBase* addonActionBarBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        var changesFound = false;

        var hotbarID = addonActionBarBase->RaptureHotbarId;

        if (hotbarID < allActionBars.Length)
        {
            changesFound = UpdateHotbarRecordedControlHints(addonActionBarBase, stringArrayData[StrDataIndex]);
            changesFound |= UpdateHotbarCommands(addonActionBarBase);
        }

        var ret = actionBarBaseUpdateHook.Original(addonActionBarBase, numberArrayData, stringArrayData);

        if (changesFound)
        {
            FillControlHints();
        }
        return ret;
    }

    private void UpdateAll()
    {
        var changesFound = UpdateHotbarRecordedControlHints();
        changesFound |= UpdateCommands();

        if (changesFound)
        {
            FillControlHints();
        }
    }

    private void Reset()
    {
        foreach (var actionBar in allActionBars)
        {
            AddonActionBarBase* ab = GetActionBarAddon(actionBar);
            if (ab != null && ab->ActionBarSlots != null)
            {
                ResetHotbar(ab);
            }
        }
        recordedControlHints = new string[allActionBars.Length][];
        recordedCommands = new HotbarSlotCommand[allActionBars.Length][];
    }

    private void ResetHotbar(AddonActionBarBase* ab)
    {
        if (ab == null || ab->ActionBarSlots == null) return;

        var numSlots = ab->SlotCount;

        for (int i = 0; i < numSlots; i++)
        {
            var slot = ab->ActionBarSlots[i];
            if (slot.ControlHintTextNode != null)
            {
                var normalControlHint = recordedControlHints?[ab->RaptureHotbarId]?[i];
                if (normalControlHint != null)
                {
                    slot.ControlHintTextNode->SetText(normalControlHint);
                }
            }
        }
    }

    private bool UpdateHotbarRecordedControlHints()
    {
        var changeFound = false;
        foreach (var actionBar in allActionBars)
        {
            AddonActionBarBase* ab = GetActionBarAddon(actionBar);
            if (ab != null && ab->ActionBarSlots != null)
            {
                changeFound |= UpdateHotbarRecordedControlHints(ab);
            }
        }
        return changeFound;
    }

    private bool UpdateHotbarRecordedControlHints(AddonActionBarBase* ab, StringArrayData* strArrayData = null)
    {
        if (ab == null || ab->ActionBarSlots == null) return false;
        if (ab->RaptureHotbarId > recordedControlHints.Length) return false;

        if (strArrayData == null)
        {
            strArrayData = Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder.StringArrays[StrDataIndex];
        }

        var numSlots = ab->SlotCount;

        var changeFound = false;

        if (recordedControlHints[ab->RaptureHotbarId] == null || recordedControlHints[ab->RaptureHotbarId].Length != numSlots || recordedControlHints[ab->RaptureHotbarId].Length != numSlots)
        {
            recordedControlHints[ab->RaptureHotbarId] = new string[numSlots];
            changeFound = true;
        }

        for (int i = 0; i < numSlots; i++)
        {
            var currentControlHint = Marshal.PtrToStringUTF8(new IntPtr(strArrayData->StringArray[(ab->RaptureHotbarId * StrDataHotbarLength + i) * StrDataSlotLength + StrDataHintIndex]));
            if (recordedControlHints?[ab->RaptureHotbarId]?[i] != currentControlHint)
            {
                recordedControlHints[ab->RaptureHotbarId][i] = currentControlHint;
                changeFound = true;
            }
        }

        return changeFound;
    }

    private bool UpdateCommands()
    {
        var changeFound = false;
        foreach (var actionBar in allActionBars)
        {
            AddonActionBarBase* ab = GetActionBarAddon(actionBar);
            if (ab != null)
            {
                changeFound |= UpdateHotbarCommands(ab);
            }
        }
        return changeFound;
    }

    private bool UpdateHotbarCommands(AddonActionBarBase* ab)
    {
        if (ab == null) return false;
        var hotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        var name = Marshal.PtrToStringUTF8(new IntPtr(ab->AtkUnitBase.Name));
        if (name == null) return false;
        var hotbar = hotbarModule->HotBar[ab->RaptureHotbarId];
        if (hotbar == null) return false;

        var numSlots = ab->SlotCount;

        var changeFound = false;

        if (recordedCommands[ab->RaptureHotbarId] == null || recordedCommands[ab->RaptureHotbarId].Length != numSlots)
        {
            recordedCommands[ab->RaptureHotbarId] = new HotbarSlotCommand[numSlots];
            changeFound = true;
        }

        for (int i = 0; i < numSlots; i++)
        {
            var command = new HotbarSlotCommand();
            var slotStruct = hotbar->Slot[i];
            if (slotStruct != null)
            {
                command.Type = slotStruct->CommandType;
                command.Id = slotStruct->CommandType == HotbarSlotType.Action ? actionManager->GetAdjustedActionId(slotStruct->CommandId) : slotStruct->CommandId;
            }

            if (!command.Equals(recordedCommands[ab->RaptureHotbarId][i]))
            {
                recordedCommands[ab->RaptureHotbarId][i] = command;
                changeFound = true;
            }
        }

        return changeFound;
    }

    private void FillControlHints()
    {
        Dictionary<HotbarSlotCommand, string> commandControlHints = new Dictionary<HotbarSlotCommand, string>(allActionBars.Length * actionBarLength);
        for (int i = 0; i < Math.Min(recordedControlHints.Length, recordedCommands.Length); i++)
        {
            if (recordedControlHints[i] == null || recordedCommands[i] == null) continue;
            for (int j = 0; j < Math.Min(recordedControlHints[i].Length, recordedCommands[i].Length); j++)
            {
                if (recordedCommands[i][j].Type != HotbarSlotType.Empty && recordedControlHints[i][j] != null && recordedControlHints[i][j].Length > 0)
                {
                    commandControlHints.TryAdd(recordedCommands[i][j], recordedControlHints[i][j]);
                }
            }
        }

        foreach (var actionBar in allActionBars)
        {
            AddonActionBarBase* ab = GetActionBarAddon(actionBar);
            if (ab != null && ab->ActionBarSlots != null)
            {
                FillHotbarControlHints(ab, commandControlHints);
            }
        }
    }

    private void FillHotbarControlHints(AddonActionBarBase* ab, Dictionary<HotbarSlotCommand, string> commandControlHints)
    {
        if (ab == null || ab->ActionBarSlots == null) return;
        var hotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        var name = Marshal.PtrToStringUTF8(new IntPtr(ab->AtkUnitBase.Name));
        if (name == null) return;
        var hotbar = hotbarModule->HotBar[ab->RaptureHotbarId];
        if (hotbar == null) return;

        for (int i = 0; i < ab->SlotCount; i++)
        {
            if (recordedCommands?[ab->RaptureHotbarId]?[i] == null) continue;

            var slot = ab->ActionBarSlots[i];
            if (slot.ControlHintTextNode != null)
            {
                var controlHint = recordedControlHints?[ab->RaptureHotbarId]?[i];
                if (controlHint == null || controlHint.Length == 0)
                {
                    var command = recordedCommands[ab->RaptureHotbarId][i];
                    if (commandControlHints.ContainsKey(command))
                    {
                        controlHint = commandControlHints[command];
                    }
                }

                slot.ControlHintTextNode->SetText(controlHint);
            }
        }
    }

    public override void Disable()
    {
        actionBarBaseUpdateHook?.Disable();
        Reset();
        base.Disable();
    }

    public override void Dispose()
    {
        actionBarBaseUpdateHook?.Dispose();
        Reset();
        base.Dispose();
    }
}