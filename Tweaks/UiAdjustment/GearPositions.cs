using System;
using System.Diagnostics.CodeAnalysis;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[Changelog("1.8.1.1", "Fixed widget display when using standard UI quality.")]
[Changelog("1.9.0.0", "Improved gamepad navigation on Character window.")]
[Changelog("1.9.1.0", "Further improved gamepad navigation on Character window.")]
public unsafe class GearPositions : UiAdjustments.SubTweak {
    public override string Name => "Adjust Equipment Positions";
    public override string Description => "Repositions equipment positions in character menu and inspect to give a less gross layout.";

    private delegate byte AddonControllerInput(AtkUnitBase* atkUnitBase, Dir a2, byte a3);
    private HookWrapper<AddonControllerInput> addonControllerInputHook;

    protected override void Enable() {
        addonControllerInputHook ??= Common.Hook<AddonControllerInput>("E8 ?? ?? ?? ?? EB B0 CC", ControllerInputDetour);
        addonControllerInputHook?.Enable();

        var bagWidget = Common.GetUnitBase("_BagWidget");
        if (bagWidget != null) BagWidgetUpdate(bagWidget);

        base.Enable();
    }

    private enum Dir : uint {
        Left = 8,
        Right = 9,
        Up = 10,
        Down = 11,
    }

    private enum CharacterNode : int {
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

    private enum CharacterInspectNode : int {
        None = -1,

        SearchInfo = 0,
        MainHand = 1,

        GCRank = 3,
        DisplayCompanyProfile = 4,

        OffHand = 7,
        Head = 8,
        Body = 9,
        Hands = 10,
        Legs = 11,
        Feet = 12,
        Ears = 13,
        Neck = 14,
        Wrist = 15,
        FingerRight = 16,
        FingerLeft = 17,
        SoulCrystal = 18,

        ResetDisplay = 20
    }

    private enum CharacterStatusNode {
        None = -1,
        Intelligence = 3,
        Mind = 4,
        
        Defense = 8,
        MagicDefense = 9,
        
        AttackMagicPotency = 12,
        HealingMagicPotency = 13,
        SpellSpeed = 14,
        
        AverageItemLevel = 19,
        Tenacity = 20,
        Piety = 21
    }

    private enum CharacterProfileNode {
        GrandCompany = 1,
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private enum CharacterClassNode {
        WHM = 5,
        SCH = 6,
        AST = 7,
        SGE = 8,
        
        BRD = 14,
        MCH = 15,
        DNC = 16,
        BLM = 17,
        SMN = 18,
        RDM = 19,
        BLU = 20,
    }

    private enum CharacterReputeNode {
        Commend = 0,
    }

    private int GetCollisionNodeIndex(AtkUnitBase* atkUnitBase) {
        for (var i = 0; i < atkUnitBase->CollisionNodeListCount; i++) {
            if (atkUnitBase->CollisionNodeList[i] == atkUnitBase->CursorTarget) {
                return i;
            }
        }
        return -1;
    }
    
    private byte F(params Enum[] t){
        byte FocusNode(AtkUnitBase* atkUnitBase, int node) {
            if (node < 0 || node >= atkUnitBase->CollisionNodeListCount) return 1;
            atkUnitBase->SetFocusNode(atkUnitBase->CollisionNodeList[node]);
            atkUnitBase->CursorTarget = atkUnitBase->CollisionNodeList[node];
            return 1;
        }

        foreach (var e in t) {
            switch (e) {
                case CharacterNode node when Common.GetUnitBase("Character", out var unitBase): return FocusNode(unitBase, (int)node);
                case CharacterStatusNode node when Common.GetUnitBase("CharacterStatus", out var unitBase): return FocusNode(unitBase, (int)node);
                case CharacterInspectNode node when Common.GetUnitBase("CharacterInspect", out var unitBase): return FocusNode(unitBase, (int)node);
                case CharacterProfileNode node when Common.GetUnitBase("CharacterProfile", out var unitBase): return FocusNode(unitBase, (int)node);
                case CharacterClassNode node when Common.GetUnitBase("CharacterClass", out var unitBase): return FocusNode(unitBase, (int)node);
                case CharacterReputeNode node when Common.GetUnitBase("CharacterRepute", out var unitBase): return FocusNode(unitBase, (int)node);
            }
        }
        return 1;
    }
    

    private byte ControllerInputDetour(AtkUnitBase* atkUnitBase, Dir d, byte a3) {
        var name = atkUnitBase->NameString;
#if DEBUG
        SimpleLog.Verbose($"{name}, {GetCollisionNodeIndex(atkUnitBase)}, {d}");
#endif
        try {
            if (atkUnitBase == Common.GetUnitBase("CharacterStatus")) {
                var currentSelectedNodeIndex = (CharacterStatusNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterStatusNode.Intelligence when d == Dir.Right => F(CharacterNode.SoulCrystal),
                    CharacterStatusNode.Mind when d == Dir.Right => F(CharacterNode.MainHand),
                    CharacterStatusNode.Defense when d == Dir.Right => F(CharacterNode.Head),
                    CharacterStatusNode.MagicDefense when d == Dir.Right => F(CharacterNode.Body),
                    CharacterStatusNode.AttackMagicPotency when d == Dir.Right => F(CharacterNode.Hands),
                    CharacterStatusNode.HealingMagicPotency when d == Dir.Right => F(CharacterNode.Hands),
                    CharacterStatusNode.SpellSpeed when d == Dir.Right => F(CharacterNode.Legs),
                    CharacterStatusNode.Tenacity when d == Dir.Right => F(CharacterNode.Feet),
                    CharacterStatusNode.Piety when d == Dir.Right => F(CharacterNode.Feet),
                    CharacterStatusNode.AverageItemLevel when d == Dir.Left => F(CharacterNode.FingerLeft),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };
            }

            if (atkUnitBase == Common.GetUnitBase("Character")) {
                var currentSelectedNodeIndex = (CharacterNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterNode.SoulCrystal when d == Dir.Right => F(CharacterNode.OffHand),
                    CharacterNode.SoulCrystal when d == Dir.Down => F(CharacterNode.MainHand),
                    CharacterNode.SoulCrystal when d == Dir.Up => F(CharacterNode.GearSetList),
                    CharacterNode.SoulCrystal when d == Dir.Left => F(CharacterStatusNode.Intelligence, CharacterProfileNode.GrandCompany, CharacterClassNode.WHM, CharacterReputeNode.Commend),
                    CharacterNode.MainHand when d == Dir.Up => F(CharacterNode.SoulCrystal),
                    CharacterNode.MainHand when d == Dir.Left => F(CharacterStatusNode.Mind, CharacterProfileNode.GrandCompany, CharacterClassNode.AST, CharacterReputeNode.Commend),
                    CharacterNode.Head when d == Dir.Right => F(CharacterNode.Ears),
                    CharacterNode.Head when d == Dir.Left => F(CharacterStatusNode.Defense, CharacterProfileNode.GrandCompany, CharacterClassNode.SGE, CharacterReputeNode.Commend),
                    CharacterNode.Body when d == Dir.Right => F(CharacterNode.Neck),
                    CharacterNode.Body when d == Dir.Left => F(CharacterStatusNode.MagicDefense, CharacterProfileNode.GrandCompany, CharacterClassNode.BRD, CharacterReputeNode.Commend),
                    CharacterNode.Hands when d == Dir.Right => F(CharacterNode.Wrist),
                    CharacterNode.Hands when d == Dir.Left => F(CharacterStatusNode.AttackMagicPotency, CharacterProfileNode.GrandCompany, CharacterClassNode.MCH, CharacterReputeNode.Commend),
                    CharacterNode.Legs when d == Dir.Right => F(CharacterNode.FingerRight),
                    CharacterNode.Legs when d == Dir.Left => F(CharacterStatusNode.SpellSpeed, CharacterProfileNode.GrandCompany, CharacterClassNode.DNC, CharacterReputeNode.Commend),
                    CharacterNode.Feet when d == Dir.Right => F(CharacterNode.FingerLeft),
                    CharacterNode.Feet when d == Dir.Left => F(CharacterStatusNode.Tenacity, CharacterProfileNode.GrandCompany, CharacterClassNode.BLM, CharacterReputeNode.Commend),
                    CharacterNode.OffHand when d == Dir.Left => F(CharacterNode.MainHand),
                    CharacterNode.Ears when d == Dir.Left => F(CharacterNode.Head),
                    CharacterNode.Neck when d == Dir.Left => F(CharacterNode.Body),
                    CharacterNode.Wrist when d == Dir.Left => F(CharacterNode.Hands),
                    CharacterNode.FingerRight when d == Dir.Left => F(CharacterNode.Legs),
                    CharacterNode.FingerLeft when d == Dir.Left => F(CharacterNode.Feet),
                    CharacterNode.FingerLeft when d == Dir.Down => F(CharacterNode.ResetDisplay),
                    CharacterNode.ResetDisplay when d == Dir.Right => F(CharacterNode.FingerLeft),
                    CharacterNode.ResetDisplay or CharacterNode.ToggleCharacterDisplayMode or CharacterNode.DrawSheathe when d == Dir.Up => F(CharacterNode.FingerLeft),
                    CharacterNode.GearSetList or CharacterNode.GlamourPlate or CharacterNode.RecommendedGear when d == Dir.Down => F(CharacterNode.SoulCrystal),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };

            }

            if (atkUnitBase == Common.GetUnitBase("CharacterInspect")) {
                var currentSelectedNodeIndex = (CharacterInspectNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterInspectNode.SearchInfo when d == Dir.Down => F(CharacterInspectNode.SoulCrystal),
                    CharacterInspectNode.SoulCrystal when d == Dir.Up => F(CharacterInspectNode.SearchInfo),
                    CharacterInspectNode.SoulCrystal when d == Dir.Down => F(CharacterInspectNode.GCRank),
                    CharacterInspectNode.SoulCrystal when d == Dir.Left => F(CharacterInspectNode.DisplayCompanyProfile),
                    CharacterInspectNode.SoulCrystal when d == Dir.Right => F(CharacterInspectNode.DisplayCompanyProfile),
                    CharacterInspectNode.DisplayCompanyProfile when d == Dir.Left => F(CharacterInspectNode.SoulCrystal),
                    CharacterInspectNode.DisplayCompanyProfile when d == Dir.Right => F(CharacterInspectNode.SoulCrystal),
                    CharacterInspectNode.GCRank when d == Dir.Up => F(CharacterInspectNode.SoulCrystal),
                    CharacterInspectNode.GCRank when d == Dir.Down => F(CharacterInspectNode.MainHand),
                    CharacterInspectNode.MainHand when d == Dir.Down => F(CharacterInspectNode.Head),
                    CharacterInspectNode.MainHand when d == Dir.Up => F(CharacterInspectNode.GCRank),
                    CharacterInspectNode.MainHand when d == Dir.Left => F(CharacterInspectNode.OffHand),
                    CharacterInspectNode.MainHand when d == Dir.Right => F(CharacterInspectNode.OffHand),
                    CharacterInspectNode.Head when d == Dir.Up => F(CharacterInspectNode.MainHand),
                    CharacterInspectNode.Head when d == Dir.Right => F(CharacterInspectNode.Ears),
                    CharacterInspectNode.Head when d == Dir.Left => F(CharacterInspectNode.Ears),
                    CharacterInspectNode.Body when d == Dir.Right => F(CharacterInspectNode.Neck),
                    CharacterInspectNode.Body when d == Dir.Left => F(CharacterInspectNode.Neck),
                    CharacterInspectNode.Hands when d == Dir.Right => F(CharacterInspectNode.Wrist),
                    CharacterInspectNode.Hands when d == Dir.Left => F(CharacterInspectNode.Wrist),
                    CharacterInspectNode.Legs when d == Dir.Right => F(CharacterInspectNode.FingerRight),
                    CharacterInspectNode.Legs when d == Dir.Left => F(CharacterInspectNode.FingerRight),
                    CharacterInspectNode.Feet when d == Dir.Right => F(CharacterInspectNode.FingerLeft),
                    CharacterInspectNode.Feet when d == Dir.Left => F(CharacterInspectNode.FingerLeft),
                    CharacterInspectNode.OffHand when d == Dir.Left => F(CharacterInspectNode.MainHand),
                    CharacterInspectNode.OffHand when d == Dir.Right => F(CharacterInspectNode.MainHand),
                    CharacterInspectNode.Ears when d == Dir.Left => F(CharacterInspectNode.Head),
                    CharacterInspectNode.Ears when d == Dir.Right => F(CharacterInspectNode.Head),
                    CharacterInspectNode.Neck when d == Dir.Left => F(CharacterInspectNode.Body),
                    CharacterInspectNode.Neck when d == Dir.Right => F(CharacterInspectNode.Body),
                    CharacterInspectNode.Wrist when d == Dir.Left => F(CharacterInspectNode.Hands),
                    CharacterInspectNode.Wrist when d == Dir.Right => F(CharacterInspectNode.Hands),
                    CharacterInspectNode.FingerRight when d == Dir.Left => F(CharacterInspectNode.Legs),
                    CharacterInspectNode.FingerRight when d == Dir.Right => F(CharacterInspectNode.Legs),
                    CharacterInspectNode.FingerLeft when d == Dir.Left => F(CharacterInspectNode.Feet),
                    CharacterInspectNode.FingerLeft when d == Dir.Right => F(CharacterInspectNode.Feet),
                    CharacterInspectNode.FingerLeft when d == Dir.Down => F(CharacterInspectNode.ResetDisplay),
                    CharacterInspectNode.ResetDisplay when d == Dir.Right => F(CharacterInspectNode.FingerLeft),
                    CharacterInspectNode.ResetDisplay when d == Dir.Up => F(CharacterInspectNode.FingerLeft),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };
            }

            if (atkUnitBase == Common.GetUnitBase("CharacterClass")) {
                var currentSelectedNodeIndex = (CharacterClassNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterClassNode.WHM when d == Dir.Right => F(CharacterNode.SoulCrystal),
                    CharacterClassNode.SCH when d == Dir.Right => F(CharacterNode.MainHand),
                    CharacterClassNode.AST when d == Dir.Right => F(CharacterNode.MainHand),
                    CharacterClassNode.SGE when d == Dir.Right => F(CharacterNode.Head),
                    CharacterClassNode.BRD when d == Dir.Right => F(CharacterNode.Body),
                    CharacterClassNode.MCH when d == Dir.Right => F(CharacterNode.Hands),
                    CharacterClassNode.DNC when d == Dir.Right => F(CharacterNode.Legs),
                    CharacterClassNode.BLM when d == Dir.Right => F(CharacterNode.Feet),
                    CharacterClassNode.SMN when d == Dir.Right => F(CharacterNode.Feet),
                    CharacterClassNode.RDM when d == Dir.Right => F(CharacterNode.Feet),
                    CharacterClassNode.BLU when d == Dir.Right => F(CharacterNode.Feet),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };
            }
            
            if (atkUnitBase == Common.GetUnitBase("CharacterProfile")) {
                var currentSelectedNodeIndex = (CharacterProfileNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterProfileNode.GrandCompany when d == Dir.Right => F(CharacterNode.SoulCrystal),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };
            }
            
            if (atkUnitBase == Common.GetUnitBase("CharacterRepute")) {
                var currentSelectedNodeIndex = (CharacterReputeNode)GetCollisionNodeIndex(atkUnitBase);
                return currentSelectedNodeIndex switch {
                    CharacterReputeNode.Commend when d == Dir.Right => F(CharacterNode.SoulCrystal),
                    _ => addonControllerInputHook.Original(atkUnitBase, d, a3)
                };
            }

            return addonControllerInputHook.Original(atkUnitBase, d, a3);
        } catch {
            return addonControllerInputHook.Original(atkUnitBase, d, a3);
        }
    }

    [AddonPostRequestedUpdate("_BagWidget")]
    private void BagWidgetUpdate(AtkUnitBase* atkUnitBase) {
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
            var backgroundImagePath = Common.GetTexturePath(backgroundImage);
            var isHighQuality = false;
            if (!string.IsNullOrEmpty(backgroundImagePath)) {
                isHighQuality = backgroundImagePath.EndsWith("_hr1.tex");
            }

            for (var i = 0U; i < 2; i++) {
                // Create
                var bgImageNode = Common.GetNodeByID<AtkImageNode>(&equipmentComponent->UldManager, CustomNodes.GearPositionsBg + i, NodeType.Image);
                if (bgImageNode == null) {
                    SimpleLog.Log($"Create Custom BG Image Node#{i}");

                    bgImageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
                    bgImageNode->AtkResNode.Type = NodeType.Image;
                    bgImageNode->AtkResNode.NodeId = CustomNodes.GearPositionsBg + i;
                    bgImageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft;
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

                    bgImageNode->LoadTexture("ui/uld/BagStatus.tex", (uint)(isHighQuality ? 2 : 1));

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
                var bgImageNode = Common.GetNodeByID<AtkImageNode>(&equipmentComponent->UldManager, CustomNodes.GearPositionsBg + i, NodeType.Image);
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

    [AddonPostSetup("CharacterInspect")]
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

    [AddonPostSetup("Character")]
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
    
    [AddonPostSetup("PvPCharacter")]
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

    private void MoveNode(AtkComponentBase* componentBase, uint NodeId, float x, float y) {
        if (componentBase == null) return;
        var node = componentBase->UldManager.SearchNodeById(NodeId);
        if (node == null) return;
        node->SetPositionFloat(x, y);
    }

    private void MoveNode(AtkUnitBase* atkUnitBase, uint NodeId, float x, float y) {
        if (atkUnitBase == null) return;
        var node = atkUnitBase->GetNodeById(NodeId);
        if (node == null) return;
        node->SetPositionFloat(x, y);
    }

    protected override void Disable() {
        var bagWidget = Common.GetUnitBase("_BagWidget");
        if (bagWidget != null) ResetBagWidget(bagWidget);
        addonControllerInputHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        addonControllerInputHook?.Dispose();
        base.Dispose();
    }
}