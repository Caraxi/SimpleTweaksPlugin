using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SimpleTweaksPlugin.Utility;
using System;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class ActionPressMirroring : UiAdjustments.SubTweak
{

    private delegate void PulseActionBarSlot(AddonActionBarBase* addonActionBarBase, int slotIndex);

    private HookWrapper<PulseActionBarSlot> pulseActionBarSlotHook;

    private static string PulseActionBarSlotSig = "85 d2 78 ?? 48 89 5c 24 ?? 57 48 83 ec ?? 48 63 da 48 8b f9 48 8b 89 ?? ?? ?? ?? ba";

    private ActionManager* actionManager;

    private bool tweakIsPulsing = false;

    public override string Name => "Duplicate Action Presses Between Hotbars";
    public override string Description => "Shows the pulse effect when activating actions, even if they are duplicated between hotbars.";
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
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
        "_ActionBarEx",
    };

    protected override void Enable()
    {
        pulseActionBarSlotHook ??= Common.Hook<PulseActionBarSlot>(PulseActionBarSlotSig, PulseActionBarSlotDetour);
        pulseActionBarSlotHook?.Enable();
        actionManager = ActionManager.Instance();
        base.Enable();
    }

    private static AddonActionBarBase* GetActionBarAddon(string actionBar)
    {
        return (AddonActionBarBase*)Service.GameGui.GetAddonByName(actionBar, 1);
    }

    private void PulseActionBarSlotDetour(AddonActionBarBase* ab, int slotIndex)
    {
        if (tweakIsPulsing) goto PulseSlot;

        var hotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        var name = Marshal.PtrToStringUTF8(new IntPtr(ab->AtkUnitBase.Name));
        if (name == null) goto PulseSlot;
        var hotbar = hotbarModule->HotBar[ab->RaptureHotbarId];
        if (hotbar == null) goto PulseSlot;
        var numSlots = ab->SlotCount;
        if (numSlots <= slotIndex) goto PulseSlot;

        tweakIsPulsing = true;

        var slot = hotbar->Slot[slotIndex];
        var commandType = slot->CommandType;
        var commandId = slot->CommandType == HotbarSlotType.Action ? actionManager->GetAdjustedActionId(slot->CommandId) : slot->CommandId;

        foreach (var actionBar in allActionBars)
        {
            var currentActionBar = (AddonActionBarBase*)Service.GameGui.GetAddonByName(actionBar, 1);
            if (currentActionBar != null)
            {
                var isCross = actionBar.StartsWith("_ActionCross");
                var crossBar = isCross ? (AddonActionCross*)currentActionBar : null;
                var crossExpandedHoldControls = isCross ? crossBar->ExpandedHoldControls : 0;
                var isExpandedCross = crossExpandedHoldControls > 0;

                var isDoubleCross = actionBar.StartsWith("_ActionDoubleCross");
                var doubleCrossBar = isDoubleCross ? (AddonActionDoubleCrossBase*)currentActionBar : null;

                numSlots = currentActionBar->SlotCount;

                int currentHotbarId = currentActionBar->RaptureHotbarId;
                if (isExpandedCross)
                {
                    // crossExpandedHoldControls value  1-16 = left/right sides of cross bars 1-8 (IDs 10-17)
                    currentHotbarId = (crossExpandedHoldControls < 17) ? (((crossExpandedHoldControls - 1) >> 1) + 10) :
                    //                                  17-20 = "Cycle" options (uses bar before/after main active bar)
                        (currentActionBar->RaptureHotbarId + (crossExpandedHoldControls < 19 ? 1 : -1) - 2) % 8 + 10;
                }
                else if (isDoubleCross)
                {
                    currentHotbarId = doubleCrossBar->BarTarget;
                }

                var currentHotbar = hotbarModule->HotBar[currentHotbarId];

                if (currentHotbar != null)
                {
                    var offset = 0;
                    var from = 0;
                    var to = numSlots;

                    if(isExpandedCross)
                    {
                        var exUseLeftSide = crossExpandedHoldControls & 1;

                        //expanded cross hotbar uses middle 8 slots with left 4 and right 4 not visible
                        offset = (exUseLeftSide != 0 ? -4 : 4);
                        from = 4;
                        to -= 4;
                    }
                    else if(isDoubleCross && doubleCrossBar->UseLeftSide == 0)
                    {
                        offset = 8;
                    }

                    for (int i = from; i < to; i++)
                    {
                        slot = currentHotbar->Slot[i + offset];
                        var currentCommandType = slot->CommandType;
                        var currentCommandId = slot->CommandType == HotbarSlotType.Action ? actionManager->GetAdjustedActionId(slot->CommandId) : slot->CommandId;
                        if (currentCommandType == commandType && currentCommandId == commandId)
                        {
                            currentActionBar->PulseActionBarSlot(i);
                        }
                    }
                }
            }
        }

        tweakIsPulsing = false;

        return;

        PulseSlot:

        pulseActionBarSlotHook.Original(ab, slotIndex);
        return;

    }

    protected override void Disable()
    {
        pulseActionBarSlotHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        pulseActionBarSlotHook?.Dispose();
        base.Dispose();
    }
}

