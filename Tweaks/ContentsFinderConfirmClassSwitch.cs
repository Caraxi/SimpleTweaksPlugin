using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Duty Ready Class Switching")]
[TweakDescription("Click 'Previous' class icon in Duty Ready window to switch class.")]
public unsafe class ContentsFinderConfirmClassSwitch : Tweak {

    private SimpleEvent simpleEvent;

    protected override void Enable() {
        simpleEvent = new SimpleEvent(OnIconClicked);
        
        if (Common.GetUnitBase("ContentsFinderConfirm", out var unitBase)) {
            var node = unitBase->GetNodeById(40);
            node->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
            if (node != null) {
                simpleEvent.Add(unitBase, node, AtkEventType.MouseClick);
                simpleEvent.Add(unitBase, node, AtkEventType.MouseOver);
                simpleEvent.Add(unitBase, node, AtkEventType.MouseOut);
            }
        }
        
        base.Enable();
    }

    private void OnIconClicked(AtkEventType eventType, AtkUnitBase* atkUnitBase, AtkResNode* node) {
        switch (eventType) {
            case AtkEventType.MouseOver: Common.ForceMouseCursor(AtkCursor.CursorType.Clickable); return;
            case AtkEventType.MouseOut: Common.UnforceMouseCursor(); return;
            case AtkEventType.MouseClick: break;
            default: return;
        }

        if (node == null) return;
        var imageNode = node->GetAsAtkImageNode();
        if (imageNode == null) return;
        var iconId = imageNode->PartsList->Parts[imageNode->PartId].UldAsset->AtkTexture.Resource->IconId;
        if (iconId is < 62100 or >= 62200) return;
        var classJobId = (uint) (iconId - 62100);
        var classJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(classJobId);
        if (classJob == null) return;
        for (var i = 0; i < 101; i++) {
            var gs = RaptureGearsetModule.Instance()->GetGearset(i);
            if (gs != null && gs->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) && gs->ClassJob == classJobId) {
                ChatHelper.SendMessage($"/gearset change {gs->Id + 1}");
                return;
            }
        }
        Service.Chat.PrintError($"You have no gearset for {classJob.Value.Name.ExtractText()}");
    }

    [AddonPostSetup("ContentsFinderConfirm")]
    private void OnAddonSetup(AtkUnitBase* addon) {
        var classIconNode = addon->GetNodeById(40);
        if (classIconNode == null) return;
        classIconNode->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
        addon->UpdateCollisionNodeList(false);
        simpleEvent.Add(addon, classIconNode, AtkEventType.MouseClick);
        simpleEvent.Add(addon, classIconNode, AtkEventType.MouseOver);
        simpleEvent.Add(addon, classIconNode, AtkEventType.MouseOut);
    }

    protected override void Disable() {
        if (Common.GetUnitBase("ContentsFinderConfirm", out var unitBase)) {
            var node = unitBase->GetNodeById(40);
            if (node != null) {
                simpleEvent.Remove(unitBase, node, AtkEventType.MouseClick);
                simpleEvent.Remove(unitBase, node, AtkEventType.MouseOver);
                simpleEvent.Remove(unitBase, node, AtkEventType.MouseOut);
            }
        }
        
        simpleEvent?.Dispose();
        base.Disable();
    }
}
