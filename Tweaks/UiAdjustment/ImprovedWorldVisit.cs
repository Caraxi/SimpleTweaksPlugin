using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets2;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Cleaner World Visit Menu")]
[TweakDescription("Cleans up the world visit menu and shows your current location in order on the list.")]
public unsafe class ImprovedWorldVisit : UiAdjustments.SubTweak {
    [AddonPostSetup("WorldTravelSelect")]
    [AddonPostRequestedUpdate("WorldTravelSelect")]
    private void SetupWorldTravelSelect(AtkUnitBase* unitBase) {
        SimpleLog.Log("Rebuild World Visit Menu");
        var currentWorld = Service.ClientState.LocalPlayer?.CurrentWorld.GameData;
        if (currentWorld == null) return;
        var currentDc = currentWorld.DataCenter.Row;
        var headerNode = unitBase->GetTextNodeById(13);
        var list = (AtkComponentList*)unitBase->GetComponentByNodeId(14);
        var currentWorldNode = unitBase->GetTextNodeById(12);
        var currentWorldIcon = unitBase->GetImageNodeById(9);

        if (headerNode == null || list == null || list->OwnerNode == null || currentWorldIcon == null || currentWorldNode == null) return;

        for (uint i = 2; i <= 16; i++) {
            if (i is 8 or 9 or 12 or 13 or 14) continue;
            var n = unitBase->GetNodeById(i);
            if (n != null) n->ToggleVisibility(false);
        }

        headerNode->SetPositionShort(16, 44);
        list->OwnerNode->SetPositionShort(23, 68);

        currentWorldNode->SetAlignment(AlignmentType.Left);
        currentWorldNode->SetWidth(164);
        currentWorldNode->SetHeight(24);
        currentWorldNode->SetXShort(25);

        var orderedWorlds = Service.Data.GetExcelSheet<World>()!.Where(w => w.DataCenter.Row == currentDc && w.IsPublic).OrderBy(w => w.Name.RawString).Select(w => w.Name.RawString).ToList();
        var currentIndex = orderedWorlds.IndexOf(currentWorld.Name.RawString);
        var pY = -26 + 24 * currentIndex;
        currentWorldNode->SetYFloat(pY);
        currentWorldIcon->SetYFloat(pY);
        currentWorldIcon->SetXFloat(-2);

        var j = 0;
        for (var i = 0; i < orderedWorlds.Count; i++) {
            if (i == currentIndex) continue;
            var listIndex = j++;
            var node = Common.GetNodeByID(&list->UldManager, (uint)(listIndex == 0 ? 5 : 51000 + listIndex));
            if (node == null) continue;
            node->SetYFloat(24 + 24 * i);
        }

        UiHelper.SetWindowSize(unitBase, (ushort)(list->OwnerNode->GetXShort() * 2 + list->OwnerNode->GetWidth()), (ushort)(list->OwnerNode->GetYShort() + list->OwnerNode->GetXShort() + 24 + 24 * orderedWorlds.Count));
    }
}
