using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using System;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class GearPositions : UiAdjustments.SubTweak {
    public override string Name => "Adjust Equipment Positions";
    public override string Description => "Repositions equipment positions in character menu and inspect to give a less gross layout.";

    private delegate void* AddonOnSetup(AtkUnitBase* atkUnitBase, int a2, void* a3);

    private HookWrapper<AddonOnSetup> characterOnSetup;
    private HookWrapper<AddonOnSetup> pvpCharacterOnSetup;
    private HookWrapper<AddonOnSetup> inspectOnSetup;
    private HookWrapper<Common.AddonOnUpdate> bagWidgetUpdate;
    
    private delegate byte AddonControllerInput(AtkUnitBase* atkUnitBase, Dir a2, byte a3);
    private HookWrapper<AddonControllerInput> addonControllerInputHook;


    public override void Enable() {
        bagWidgetUpdate ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 4C 8B 62 38", BagWidgetUpdate);
        bagWidgetUpdate?.Enable();
        
        Common.AddonSetup += OnAddonSetup;
        addonControllerInputHook ??= Common.Hook<AddonControllerInput>("E8 ?? ?? ?? ?? EB B0 CC", ControllerInputDetour);
        addonControllerInputHook?.Enable();

        var bagWidget = Common.GetUnitBase("_BagWidget");
        if (bagWidget != null) BagWidgetUpdate(bagWidget, null, null);

        base.Enable();
    }

    private void OnAddonSetup(SetupAddonArgs obj) {
        switch (obj.AddonName) {
            case "Character": CharacterOnSetup(obj.Addon); break;
            case "PvPCharacter": PvpCharacterOnSetup(obj.Addon); break;
            case "CharacterInspect": InspectOnSetup(obj.Addon); break;
        }
    }

    private enum Dir : uint {
        Left = 8,
        Right = 9,
        Up = 10,
        Down = 11,
    }

    private enum Node : int {
        None = -1,
        
        RecommendedGear = 4,
        GlamourPlate = 5,
        GearSetList = 6,
        
        MainHand = 8,
        OffHand = 9,
        Head = 10,
        Body = 11,
        Hands = 13,
        Legs = 12,
        Feet = 14,
        Ears = 15,
        Neck = 16,
        Wrist = 17,
        FingerRight = 18,
        FingerLeft = 19,
        SoulCrystal = 20,
        
        DrawSheathe = 25,
        ToggleCharacterDisplayMode = 26,
        ResetDisplay = 27,
        
    }

    private int GetCollisionNodeIndex(AtkUnitBase* atkUnitBase) {
        for (var i = 0; i < atkUnitBase->CollisionNodeListCount; i++) {
            if (atkUnitBase->CollisionNodeList[i] == atkUnitBase->CursorTarget) {
                return i;
            }
        }
        return -1;
    }
    
    
    
    private byte ControllerInputDetour(AtkUnitBase* atkUnitBase, Dir d, byte a3) {
        try {
            if (atkUnitBase != Common.GetUnitBase("Character")) return addonControllerInputHook.Original(atkUnitBase, d, a3);

            var currentSelectedNodeIndex = (Node)GetCollisionNodeIndex(atkUnitBase);

            byte F(Node node) {
                if ((int)node < 0 || (int)node >= atkUnitBase->CollisionNodeListCount) return 1;
                atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[(int)node]);
                atkUnitBase->CursorTarget = atkUnitBase->CollisionNodeList[(int)node];
                return 1;
            }

            return currentSelectedNodeIndex switch {
                Node.SoulCrystal when d == Dir.Right => F(Node.OffHand),
                Node.SoulCrystal when d == Dir.Down => F(Node.MainHand),
                Node.SoulCrystal when d == Dir.Up => F(Node.GearSetList),
                Node.SoulCrystal when d == Dir.Left => F(Node.MainHand),
                Node.MainHand when d == Dir.Up => F(Node.SoulCrystal),
                Node.Head when d == Dir.Right => F(Node.Ears),
                Node.Body when d == Dir.Right => F(Node.Neck),
                Node.Hands when d == Dir.Right => F(Node.Wrist),
                Node.Legs when d == Dir.Right => F(Node.FingerRight),
                Node.Feet when d == Dir.Right => F(Node.FingerLeft),
                Node.OffHand when d == Dir.Left => F(Node.MainHand),
                Node.Ears when d == Dir.Left => F(Node.Head),
                Node.Neck when d == Dir.Left => F(Node.Body),
                Node.Wrist when d == Dir.Left => F(Node.Hands),
                Node.FingerRight when d == Dir.Left => F(Node.Legs),
                Node.FingerLeft when d == Dir.Left => F(Node.Feet),
                Node.FingerLeft when d == Dir.Down => F(Node.ResetDisplay),
                Node.ResetDisplay when d == Dir.Right => F(Node.FingerLeft),
                Node.ResetDisplay or Node.ToggleCharacterDisplayMode or Node.DrawSheathe when d == Dir.Up => F(Node.FingerLeft),
                Node.GearSetList or Node.GlamourPlate or Node.RecommendedGear when d == Dir.Down => F(Node.SoulCrystal),
                _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
            };
        } catch {
            return addonControllerInputHook.Original(atkUnitBase, d, a3);
        }
        
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
            for (var i = 0U; i < 2; i++) {
                // Create
                var bgImageNode = Common.GetNodeByID<AtkImageNode>(equipmentComponent->UldManager, CustomNodes.GearPositionsBg + i, NodeType.Image);
                if (bgImageNode == null) {
                    SimpleLog.Log($"Create Custom BG Image Node#{i}");

                    bgImageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
                    bgImageNode->AtkResNode.Type = NodeType.Image;
                    bgImageNode->AtkResNode.NodeID = CustomNodes.GearPositionsBg + i;
                    bgImageNode->AtkResNode.Flags = (short)(NodeFlags.AnchorTop | NodeFlags.AnchorLeft);
                    bgImageNode->AtkResNode.DrawFlags = 0;
                    bgImageNode->WrapMode = 1;
                    bgImageNode->Flags = 0;

                    var partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
                    if (partsList == null) {
                        SimpleLog.Error("Failed to alloc memory for parts list.");
                        bgImageNode->AtkResNode.Destroy(true);
                        break;
                    }

                    partsList->Id = 0;
                    partsList->PartCount = 1;

                    var part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);
                    if (part == null) {
                        SimpleLog.Error("Failed to alloc memory for part.");
                        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                        bgImageNode->AtkResNode.Destroy(true);
                        break;
                    }

                    part->U = 21;
                    part->V = 13;
                    part->Width = 11;
                    part->Height = 41;

                    partsList->Parts = part;

                    var asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);
                    if (asset == null) {
                        SimpleLog.Error("Failed to alloc memory for asset.");
                        IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
                        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
                        bgImageNode->AtkResNode.Destroy(true);
                        break;
                    }

                    asset->Id = 0;
                    asset->AtkTexture.Ctor();
                    part->UldAsset = asset;
                    bgImageNode->PartsList = partsList;

                    bgImageNode->LoadTexture("ui/uld/BagStatus.tex");

                    bgImageNode->AtkResNode.ToggleVisibility(true);

                    bgImageNode->AtkResNode.SetWidth(11);
                    bgImageNode->AtkResNode.SetHeight(41);
                    bgImageNode->AtkResNode.SetPositionShort((short)(i == 0 ? 3 : 21), 10);


                    var prev = backgroundImage->AtkResNode.PrevSiblingNode;
                    bgImageNode->AtkResNode.ParentNode = backgroundImage->AtkResNode.ParentNode;

                    backgroundImage->AtkResNode.PrevSiblingNode = (AtkResNode*)bgImageNode;
                    prev->NextSiblingNode = (AtkResNode*)bgImageNode;

                    bgImageNode->AtkResNode.PrevSiblingNode = prev;
                    bgImageNode->AtkResNode.NextSiblingNode = (AtkResNode*)backgroundImage;

                    equipmentComponent->UldManager.UpdateDrawNodeList();
                }
            }
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

            for (var i = 0U; i < 2; i++) {
                var bgImageNode = Common.GetNodeByID<AtkImageNode>(equipmentComponent->UldManager, CustomNodes.GearPositionsBg + i, NodeType.Image);
                if (bgImageNode != null) {
                    if (bgImageNode->AtkResNode.PrevSiblingNode != null)
                        bgImageNode->AtkResNode.PrevSiblingNode->NextSiblingNode = bgImageNode->AtkResNode.NextSiblingNode;
                    if (bgImageNode->AtkResNode.NextSiblingNode != null)
                        bgImageNode->AtkResNode.NextSiblingNode->PrevSiblingNode = bgImageNode->AtkResNode.PrevSiblingNode;
                    equipmentComponent->UldManager.UpdateDrawNodeList();

                    IMemorySpace.Free(bgImageNode->PartsList->Parts->UldAsset, (ulong)sizeof(AtkUldPart));
                    IMemorySpace.Free(bgImageNode->PartsList->Parts, (ulong)sizeof(AtkUldPart));
                    IMemorySpace.Free(bgImageNode->PartsList, (ulong)sizeof(AtkUldPartsList));
                    bgImageNode->AtkResNode.Destroy(true);
                }
            }
        }
    }

    private void InspectOnSetup(AtkUnitBase* atkUnitBase) {
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
    }

    private void CharacterOnSetup(AtkUnitBase* atkUnitBase) {
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

        // Glamour Error Icons
        MoveNode(atkUnitBase, 32, 18, 25); // Job Stone
        MoveNode(atkUnitBase, 20, 18, 86); // Main Hand
        MoveNode(atkUnitBase, 22, 18, 133); // Head
        MoveNode(atkUnitBase, 23, 18, 180); // Body
        MoveNode(atkUnitBase, 25, 18, 227); // Hands
        MoveNode(atkUnitBase, 24, 18, 274); // Legs
        MoveNode(atkUnitBase, 26, 18, 321); // Feet
    }
    
    private void PvpCharacterOnSetup(AtkUnitBase* atkUnitBase) {
        
        // Slots
        MoveNode(atkUnitBase, 126, 0, -1); // Job Stone
        MoveNode(atkUnitBase, 114, -8, 60); // Main Weapon
        MoveNode(atkUnitBase, 116, -8, 107); // Head
        MoveNode(atkUnitBase, 117, -8, 154); // Body
        MoveNode(atkUnitBase, 119, -8, 201); // Hands
        MoveNode(atkUnitBase, 118, -8, 248); // Legs
        MoveNode(atkUnitBase, 120, -8, 295); // Feet
        
        // Images
        MoveNode(atkUnitBase, 112, 0, 0); // Job Stone
        MoveNode(atkUnitBase, 102, 0, 108); // Head
        MoveNode(atkUnitBase, 103, 0, 155); // Body
        MoveNode(atkUnitBase, 104, 0, 202); // Hands
        MoveNode(atkUnitBase, 105, 0, 249); // Legs
        MoveNode(atkUnitBase, 106, 0, 296); // Feet
        
        // Glamour Error Icons
        MoveNode(atkUnitBase, 98, 18, 25); // Job Stone
        MoveNode(atkUnitBase, 86, 18, 86); // Main Hand
        MoveNode(atkUnitBase, 88, 18, 133); // Head
        MoveNode(atkUnitBase, 89, 18, 180); // Body
        MoveNode(atkUnitBase, 91, 18, 227); // Hands
        MoveNode(atkUnitBase, 90, 18, 274); // Legs
        MoveNode(atkUnitBase, 92, 18, 321); // Feet
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
        bagWidgetUpdate?.Disable();
        addonControllerInputHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        bagWidgetUpdate?.Dispose();
        addonControllerInputHook?.Dispose();
        base.Dispose();
    }
}