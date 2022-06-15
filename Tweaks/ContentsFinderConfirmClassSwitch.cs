using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class ContentsFinderConfirmClassSwitch : Tweak {
    public override string Name => "Duty Ready Class Switching";
    public override string Description => "Click 'Previous' class icon in Duty Ready window to switch class.";

    private SimpleEvent simpleEvent;

    public override void Enable() {
        simpleEvent = new SimpleEvent(OnIconClicked);
        
        if (Common.GetUnitBase("ContentsFinderConfirm", out var unitBase)) {
            var node = unitBase->GetNodeById(40);
            node->Flags |= (short)(NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision);
            if (node != null) {
                simpleEvent.Add(unitBase, node, AtkEventType.MouseClick);
                simpleEvent.Add(unitBase, node, AtkEventType.MouseOver);
                simpleEvent.Add(unitBase, node, AtkEventType.MouseOut);
            }
        }

        Common.AddonSetup += OnAddonSetup;
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
        var iconId = imageNode->PartsList->Parts[imageNode->PartId].UldAsset->AtkTexture.Resource->IconID;
        if (iconId is < 62100 or >= 62200) return;
        var classJobId = (uint) (iconId - 62100);
        var classJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(classJobId);
        if (classJob == null) return;
        for (var i = 0; i < 101; i++) {
            var gs = RaptureGearsetModule.Instance()->Gearset[i];
            if (gs->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) && gs->ClassJob == classJobId) {
                Plugin.XivCommon.Functions.Chat.SendMessage($"/gearset change {gs->ID + 1}");
                return;
            }
        }
        Service.Chat.PrintError($"You have no gearset for {classJob.Name.RawString}");
    }

    private void OnAddonSetup(SetupAddonArgs obj) {
        if (obj.AddonName != "ContentsFinderConfirm") return;
        var classIconNode = obj.Addon->GetNodeById(40);
        if (classIconNode == null) return;
        classIconNode->Flags |= (short)(NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision);
        obj.Addon->UpdateCollisionNodeList(false);
        simpleEvent.Add(obj.Addon, classIconNode, AtkEventType.MouseClick);
        simpleEvent.Add(obj.Addon, classIconNode, AtkEventType.MouseOver);
        simpleEvent.Add(obj.Addon, classIconNode, AtkEventType.MouseOut);
    }

    public override void Disable() {
        Common.AddonSetup -= OnAddonSetup;

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
