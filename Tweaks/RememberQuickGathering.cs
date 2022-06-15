using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using System;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.Tweaks;
public unsafe class RememberQuickGathering : Tweak {
    public override string Name => "Remember Quick Gathering";
    public override string Description => "Remembers quick gathering status even after gathering at unspoiled nodes.";

    private delegate void ToggleQuickGathering(AddonGathering* _this);

    private ToggleQuickGathering toggleQuickGathering;
    private bool? targetQuickGatheringStatus;
    private bool wasAddonOpen;
    private bool shouldUpdateCheckmark;

    public override void Setup() {
        if (Ready) return;

        try {
            if (toggleQuickGathering is null) {
                var toggleQuickGatheringPtr = Service.SigScanner.ScanText("e8 ?? ?? ?? ?? eb 3f 4c 8b 4c 24 50");
                toggleQuickGathering = Marshal.GetDelegateForFunctionPointer<ToggleQuickGathering>(toggleQuickGatheringPtr);
            }

            base.Setup();
        } catch (Exception ex) {
            SimpleLog.Error($"Failed to setup {this.GetType().Name}: {ex.Message}");
        }
    }

    public override void Enable() {
        Service.Framework.Update += FrameworkUpdate;
        base.Enable();
    }

    private void FrameworkUpdate(Dalamud.Game.Framework framework) {
        var addon = Common.GetUnitBase<AddonGathering>("Gathering");
        if (addon is not null && IsGatheringPointLoaded(addon)) {
            if (CanQuickGather(addon)) {
                if (targetQuickGatheringStatus.HasValue) {
                    if (!wasAddonOpen) {
                        TrySetQuickGathering(addon);
                    }
                    if (shouldUpdateCheckmark) {
                        TryUpdateCheckmark(addon);
                    }
                }

                targetQuickGatheringStatus = addon->QuickGatheringComponentCheckBox->IsChecked;
            }
            wasAddonOpen = true;
        } else {
            wasAddonOpen = false;
            shouldUpdateCheckmark = false;
        }
    }

    private bool CanQuickGather(AddonGathering* addon) {
        // Use visibility of quick gathering checkbox as indicator
        return addon->QuickGatheringComponentCheckBox->AtkComponentButton.AtkComponentBase.OwnerNode->AtkResNode.IsVisible;
    }
    private void TrySetQuickGathering(AddonGathering* addon) {
        // Only toggle if not already in target state
        if (addon->QuickGatheringComponentCheckBox->IsChecked != targetQuickGatheringStatus) {
            addon->QuickGatheringComponentCheckBox->AtkComponentButton.Flags ^= 0x40000;
            toggleQuickGathering(addon);
            shouldUpdateCheckmark = true;
        }
    }
    private void TryUpdateCheckmark(AddonGathering* addon) {
        // It takes a few attempts for these changes to stick, haven't looked into why so we just keep trying.

        // I don't know if there's a better way to get the checkmark image node
        AtkResNode* checkmarkNode = addon->QuickGatheringComponentCheckBox->AtkComponentButton.ButtonBGNode->PrevSiblingNode;
        if (checkmarkNode->IsVisible == targetQuickGatheringStatus) {
            // Finally, success
            shouldUpdateCheckmark = false;
        } else {
            checkmarkNode->Color.A = (byte)(targetQuickGatheringStatus.Value ? 0xFF : 0x7F);
            checkmarkNode->Flags ^= 0x10;
        }
    }

    private static bool IsGatheringPointLoaded(AddonGathering* addon) {
        // At least one item slot needs to have an item ID
        return addon->GatheredItemId1 != 0
                || addon->GatheredItemId2 != 0
                || addon->GatheredItemId3 != 0
                || addon->GatheredItemId4 != 0
                || addon->GatheredItemId5 != 0
                || addon->GatheredItemId6 != 0
                || addon->GatheredItemId7 != 0
                || addon->GatheredItemId8 != 0;
    }

    public override void Disable() {
        targetQuickGatheringStatus = null;
        Service.Framework.Update -= FrameworkUpdate;
        base.Disable();
    }
}
