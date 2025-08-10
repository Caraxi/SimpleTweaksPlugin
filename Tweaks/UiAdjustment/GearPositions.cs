using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[Changelog("1.8.1.1", "Fixed widget display when using standard UI quality.")]
[Changelog("1.9.0.0", "Improved gamepad navigation on Character window.")]
[Changelog("1.9.1.0", "Further improved gamepad navigation on Character window.")]
[Changelog("1.10.11.1", "Fixed incorrect ordering of equipment.")]
[TweakAutoConfig]
[TweakName("Adjust Equipment Positions")]
[TweakDescription("Repositions equipment positions in character menu and inspect to improve appearance.")]
public unsafe class GearPositions : UiAdjustments.SubTweak {
    // TODO:
    // - Bag Widget
    // - PvP Profile Character Page
    // - Remap controller navigation

    public class Configs : TweakConfig {
        [TweakConfigOption("Soulstone Above Offhand")]
        public bool SoulstoneAboveOffhand = true;
    }

    public Configs TweakConfig { get; private set; }

    [AddonPostSetup("CharacterInspect")]
    private void InspectOnSetup(AtkUnitBase* atkUnitBase) {
        if (TweakConfig.SoulstoneAboveOffhand) {
            MoveNode(atkUnitBase, 54, 262, -119);
            MoveNode(atkUnitBase, 68, 262, -119);
        } else {
            ShiftUp(atkUnitBase, 43, 262, -119, 49, 50, 51, 52, 53, 54);
            ShiftUp(atkUnitBase, 57, 262, -119, 63, 64, 65, 66, 67, 68);
        }
    }

    [AddonPostSetup("Character")]
    private void CharacterOnSetup(AtkUnitBase* atkUnitBase) {
        if (TweakConfig.SoulstoneAboveOffhand) {
            MoveNode(atkUnitBase, 61, 262, -1);
            MoveNode(atkUnitBase, 46, 262, 0);
            MoveNode(atkUnitBase, 32, 280, 25);
        } else {
            ShiftUp(atkUnitBase, 50, 262, -1, 56, 57, 58, 59, 60, 61);
            ShiftUp(atkUnitBase, 35, 262, 0, 41, 42, 43, 44, 45, 46);
            ShiftUp(atkUnitBase, 21, 280, 25, 27, 28, 29, 30, 31, 32);
        }
    }

    private void ShiftUp(AtkUnitBase* atkUnitBase, uint firstNodeId, float firstNodeX, float firstNodeY, params uint[] moveNodes) {
        if (atkUnitBase == null) return;
        var firstNode = atkUnitBase->GetNodeById(firstNodeId);
        if (firstNode == null) return;

        var p = new (ulong node, float x, float y)[moveNodes.Length];
        var nP = firstNode;
        for (var i = 0; i < p.Length; i++) {
            var n = atkUnitBase->GetNodeById(moveNodes[i]);
            if (n == null) return;
            p[i] = ((ulong)n, nP->X, nP->Y);
            nP = n;
        }

        firstNode->SetPositionFloat(firstNodeX, firstNodeY);
        for (var i = 0; i < p.Length; i++) {
            var node = (AtkResNode*)p[i].node;
            node->SetPositionFloat(p[i].x, p[i].y);
        }
    }

    private void MoveNode(AtkUnitBase* atkUnitBase, uint nodeId, float x, float y) {
        if (atkUnitBase == null) return;
        var node = atkUnitBase->GetNodeById(nodeId);
        if (node == null) return;
        node->SetPositionFloat(x, y);
    }
}
