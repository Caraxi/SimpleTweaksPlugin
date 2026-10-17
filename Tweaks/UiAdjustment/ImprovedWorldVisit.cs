using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Cleaner World Visit Menu")]
[TweakDescription("Cleans up the world visit menu and shows your current location in order on the list.")]
[Changelog("1.15.0.8", "Fixed display on worlds that allow cross-DC travel")]
public unsafe class ImprovedWorldVisit : Tweak {
    [AddonEvent("WorldTravelSelect", AddonEvent.PostSetup, AddonEvent.PostRequestedUpdate, AddonEvent.PostRefresh, AddonEvent.PostOpen)]
    private void SetupWorldTravelSelect(AtkUnitBase* unitBase) {
        if (Service.KeyState[VirtualKey.SHIFT]) return;
        var dcSelectNode = unitBase->GetNodeById(2);
        if (dcSelectNode == null) return;
        var isDcSelectAvailable = dcSelectNode->IsVisible();
        SimpleLog.Log($"Rebuild World Visit Menu (CrossDC:{isDcSelectAvailable})");
        var list = unitBase->GetComponentListById(isDcSelectAvailable ? 7U : 20U );
        if (list == null || list->OwnerNode == null) return;
        for (var i = 2U; i <= 22; i++) {
            var n = unitBase->GetNodeById(i);
            if (n != null) n->ToggleVisibility(isDcSelectAvailable ? i <= 7 : i == list->OwnerNode->NodeId);
        }

        if (isDcSelectAvailable) {
            dcSelectNode->SetPositionShort(16, 44);
            UiHelper.SetWindowSize(unitBase, (ushort)(dcSelectNode->GetXShort() * 2 + dcSelectNode->GetWidth()), (ushort)(dcSelectNode->GetYShort() + 52 + 24 * Math.Clamp(list->GetItemCount(), 8, list->NumVisibleItems)));
        } else {
            list->OwnerNode->SetPositionShort(16, 44);
            UiHelper.SetWindowSize(unitBase, (ushort)(list->OwnerNode->GetXShort() * 2 + list->OwnerNode->GetWidth()), (ushort)(list->OwnerNode->GetYShort() + list->OwnerNode->GetXShort() + 216));
        }
    }
}
