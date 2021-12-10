using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class GearPositions : UiAdjustments.SubTweak {
        public override string Name => "Adjust Equipment Positions";
        public override string Description => "Repositions equipment positions in character menu and inspect to give a less gross layout.";

        private delegate void* AddonOnSetup(AtkUnitBase* atkUnitBase, int a2, void* a3);

        private HookWrapper<AddonOnSetup> characterOnSetup;
        private HookWrapper<AddonOnSetup> inspectOnSetup;
        private HookWrapper<Common.AddonOnUpdate> bagWidgetUpdate;

        public override void Enable() {
            characterOnSetup ??= Common.Hook<AddonOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 60 4D 8B F0", CharacterOnSetup);
            inspectOnSetup ??= Common.Hook<AddonOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B F9 48 8B D1", InspectOnSetup);
            bagWidgetUpdate ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B 62 38", BagWidgetUpdate);
            characterOnSetup?.Enable();
            inspectOnSetup?.Enable();
            bagWidgetUpdate?.Enable();

            var bagWidget = Common.GetUnitBase("_BagWidget");
            if (bagWidget != null) BagWidgetUpdate(bagWidget, null, null);

            base.Enable();
        }

        private void BagWidgetUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
            var equipmentComponentNode = atkUnitBase->GetNodeById(6);
            if (equipmentComponentNode == null) return;
            if ((ushort)equipmentComponentNode->Type < 1000) return;
            var equipmentComponent = ((AtkComponentNode*)equipmentComponentNode)->Component;
            if (equipmentComponent == null) return;
            MoveNode(equipmentComponent, 3, 5, 10);
            MoveNode(equipmentComponent, 4, 23, 10);
            for (var i = 5U; i < 10; i++) MoveNode(equipmentComponent, i, 5, 10 + (i - 4) * 6);
            for (var i = 10U; i < 15; i++) MoveNode(equipmentComponent, i, 23, 10 + (i - 9) * 6);

            var backgroundImage = (AtkImageNode*) equipmentComponent->UldManager.SearchNodeById(15);
            if (backgroundImage != null) {
                backgroundImage->AtkResNode.ToggleVisibility(false);
            }
        }

        private void ResetBagWidget(AtkUnitBase* atkUnitBase) {
            var equipmentComponentNode = atkUnitBase->GetNodeById(6);
            if (equipmentComponentNode == null) return;
            if ((ushort)equipmentComponentNode->Type < 1000) return;
            var equipmentComponent = ((AtkComponentNode*)equipmentComponentNode)->Component;
            if (equipmentComponent == null) return;
            MoveNode(equipmentComponent, 3, 5, 5);
            MoveNode(equipmentComponent, 4, 23, 13);
            for (var i = 5U; i < 10; i++) MoveNode(equipmentComponent, i, 5, 13 + (i - 5) * 6);
            for (var i = 10U; i < 15; i++) MoveNode(equipmentComponent, i, 23, 19 + (i - 10) * 6);

            var backgroundImage = (AtkImageNode*) equipmentComponent->UldManager.SearchNodeById(15);
            if (backgroundImage != null) {
                backgroundImage->AtkResNode.ToggleVisibility(true);
            }
        }

        private void* InspectOnSetup(AtkUnitBase* atkUnitBase, int a2, void* a3) {
            var retVal = inspectOnSetup.Original(atkUnitBase, a2, a3);

            // Slots
            MoveNode(atkUnitBase, 47, 0, -120); // Job Stone
            MoveNode(atkUnitBase, 12, 9, 125); // Main Weapon
            MoveNode(atkUnitBase, 37, 0, 46 * 1); // Head
            MoveNode(atkUnitBase, 38, 0, 46 * 2); // Body
            MoveNode(atkUnitBase, 39, 0, 46 * 3); // Hands
            MoveNode(atkUnitBase, 40, 0, 46 * 4); // Legs
            MoveNode(atkUnitBase, 41, 0, 46 * 5); // Feet

            // Images
            MoveNode(atkUnitBase, 60, 0, -120); // Job Stone
            MoveNode(atkUnitBase, 13, 15, 130); // Main Weapon
            MoveNode(atkUnitBase, 50, 0, 46 * 1); // Head
            MoveNode(atkUnitBase, 51, 0, 46 * 2); // Body
            MoveNode(atkUnitBase, 52, 0, 46 * 3); // Hands
            MoveNode(atkUnitBase, 53, 0, 46 * 4); // Legs
            MoveNode(atkUnitBase, 54, 0, 46 * 5); // Feet

            return retVal;
        }

        private void* CharacterOnSetup(AtkUnitBase* atkUnitBase, int a2, void* a3) {
            var retVal = characterOnSetup.Original(atkUnitBase, a2, a3);

            // Slots
            MoveNode(atkUnitBase, 60, 0, -1); // Job Stone
            MoveNode(atkUnitBase, 48, -8, 60); // Main Weapon
            MoveNode(atkUnitBase, 50, -8, 107); // Head
            MoveNode(atkUnitBase, 51, -8, 154); // Body
            MoveNode(atkUnitBase, 53, -8, 201); // Hands
            MoveNode(atkUnitBase, 52, -8, 248); // Legs
            MoveNode(atkUnitBase, 54, -8, 295); // Feet

            // Images
            MoveNode(atkUnitBase, 46, 0, 0);
            MoveNode(atkUnitBase, 36, 0, 108); // Head
            MoveNode(atkUnitBase, 37, 0, 155); // Body
            MoveNode(atkUnitBase, 38, 0, 202); // Hands
            MoveNode(atkUnitBase, 39, 0, 249); // Legs
            MoveNode(atkUnitBase, 40, 0, 296); // Feet

            return retVal;
        }

        private void MoveNode(AtkComponentBase* componentBase, uint nodeId, float x, float y) {
            if (componentBase == null) return;
            var node = componentBase->UldManager.SearchNodeById(nodeId);
            if (node == null) return;
            node->SetPositionFloat(x, y);
        }

        private void MoveNode(AtkUnitBase* atkUnitBase, uint nodeId, float x, float y) {
            if (atkUnitBase == null) return;
            var node = atkUnitBase->GetNodeById(nodeId);
            if (node == null) return;
            node->SetPositionFloat(x, y);
        }

        public override void Disable() {
            var bagWidget = Common.GetUnitBase("_BagWidget");
            if (bagWidget != null) ResetBagWidget(bagWidget);
            characterOnSetup?.Disable();
            inspectOnSetup?.Disable();
            bagWidgetUpdate?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            characterOnSetup?.Dispose();
            inspectOnSetup?.Dispose();
            bagWidgetUpdate?.Dispose();
            base.Dispose();
        }
    }
}
