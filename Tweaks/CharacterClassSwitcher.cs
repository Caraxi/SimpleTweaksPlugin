using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Character Window Job Switcher")]
[TweakDescription("Allow clicking on classes to switch to gearsets.")]
[Changelog("1.8.5.1", "Fixed tweak not working on DoH without desynthesis unlocked.")]
[Changelog("1.15.0.6", "Fixed issue causing a loop of trying to apply a gearset that is missing its weapon when using gamepad.")]
public unsafe class CharacterClassSwitcher : Tweak {
    private readonly Dictionary<uint, uint> classJobComponentMap = new() {
        { 19, 08 }, // PLD
        { 21, 10 }, // WAR
        { 32, 12 }, // DRK
        { 37, 14 }, // GNB

        { 24, 20 }, // WHM
        { 28, 22 }, // SCH
        { 33, 24 }, // AST
        { 40, 26 }, // SGE

        { 20, 32 }, // MNK
        { 22, 34 }, // DRG
        { 30, 36 }, // NIN
        { 34, 38 }, // SAM
        { 39, 40 }, // RPR
        { 41, 42 }, // VPR
        { 43, 44 }, // BST

        { 23, 50 }, // BRD
        { 31, 52 }, // MCH
        { 38, 54 }, // DNC

        { 25, 60 }, // BLM
        { 27, 62 }, // SMN
        { 35, 64 }, // RDM
        { 42, 66 }, // PCT
        { 36, 68 }, // BLU

        { 08, 73 }, // CRP
        { 09, 74 }, // BSM
        { 10, 75 }, // ARM
        { 11, 76 }, // GSM
        { 12, 77 }, // LTW
        { 13, 78 }, // WVR
        { 14, 79 }, // ALC
        { 15, 80 }, // CUL

        { 16, 86 }, // MIN
        { 17, 88 }, // BTN
        { 18, 90 }, // FSH
    };

    [AddonPostSetup("CharacterClass")]
    private void SetupCharacterClass(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase != null) {
            SimpleLog.Log("Setup CharacterClass Events");
            foreach (var (cjId, nodeId) in classJobComponentMap) {
                var componentNode = (AtkComponentNode*)atkUnitBase->GetNodeById(nodeId);
                if (componentNode == null) continue;

                switch (componentNode->AtkResNode.Type) {
                    case (NodeType)1001: {
                        // DoH
                        var colNode = Common.GetNodeByID<AtkCollisionNode>(&componentNode->Component->UldManager, 12, NodeType.Collision);
                        if (colNode != null) {
                            var evt = colNode->AtkResNode.AtkEventManager.Event;
                            while (evt != null) {
                                if (evt->State.EventType is AtkEventType.MouseClick or AtkEventType.InputReceived) {
                                    evt->Param = 0x53541000 + cjId;
                                    evt->Listener = (AtkEventListener*)atkUnitBase;
                                }

                                evt = evt->NextEvent;
                            }
                        }

                        break;
                    }
                    case (NodeType)1003: {
                        // Others
                        var colNode = (AtkCollisionNode*)componentNode->Component->UldManager.SearchNodeById(8);
                        if (colNode == null) continue;
                        if (colNode->AtkResNode.Type != NodeType.Collision) continue;

                        colNode->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53541000 + cjId, (AtkEventListener*)atkUnitBase, (AtkResNode*)colNode, false);
                        colNode->AtkResNode.AddEvent(AtkEventType.InputReceived, 0x53541000 + cjId, (AtkEventListener*)atkUnitBase, (AtkResNode*)colNode, false);
                        break;
                    }
                }
            }

            var dohIconImage = atkUnitBase->GetImageNodeById(70);
            if (dohIconImage != null) {
                dohIconImage->AtkResNode.NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse;
                dohIconImage->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*)atkUnitBase, (AtkResNode*)dohIconImage, false);
            }

            var dohHeaderText = atkUnitBase->GetTextNodeById(71);
            if (dohHeaderText != null) {
                dohHeaderText->AtkResNode.NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse;
                dohHeaderText->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*)atkUnitBase, (AtkResNode*)dohIconImage, false);
            }

            SimpleLog.Log("CharacterClass Events Setup");
        }
    }

    private (int InputId, InputState State) lastControllerInput;
    [AddonPreReceiveEvent("CharacterClass")]
    private void EventHandle(AddonReceiveEventArgs args) {
        var eventType = (AtkEventType)args.AtkEventType;
        var eventParam = args.EventParam;

        if (eventType == AtkEventType.MouseClick && eventParam == 0x53542000) {
            // Open Desynthesis Skill Window
            args.AtkEventType = AddonEventType.ButtonClick;
            args.EventParam = 22;
            return;
        }

        if (eventType == AtkEventType.InputReceived) {
            var a5 = (AtkEventData*)args.AtkEventData;
            if (a5 == null) return;
            try {
                if (a5->InputData.InputId != 0x01 || a5->InputData.State != InputState.Up || lastControllerInput.InputId != 0x01 || lastControllerInput.State is not (InputState.Down or InputState.Held)) return;
            } finally {
                lastControllerInput = (a5->InputData.InputId, a5->InputData.State);
            }
        }

        try {
            if (eventType is AtkEventType.MouseClick or AtkEventType.ButtonClick or AtkEventType.InputReceived && (eventParam & 0x53541000) == 0x53541000) {
                var cjId = (uint)(eventParam - 0x53541000);
                if (!Service.Data.Excel.GetSheet<ClassJob>().TryGetRow(cjId, out var classJob)) return;
                SimpleLog.Debug($"Change Class: ClassJob#{cjId} ({classJob.Abbreviation.ExtractText()})");

                var gearsetId = GetGearsetForClassJob(classJob);
                if (gearsetId != null) {
                    SimpleLog.Log($"Send Command: /gearset change {gearsetId.Value + 1}");
                    ChatHelper.SendMessage($"/gearset change {gearsetId.Value + 1}");
                } else {
                    Service.Chat.PrintError($"No saved gearset for {classJob.Name.ExtractText()}");
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private byte? GetGearsetForClassJob(ClassJob cj) {
        byte? backup = null;
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++) {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->Id != i) continue;
            if (gearset->ClassJob == cj.RowId) return gearset->Id;
            if (backup == null && cj.ClassJobParent.RowId != 0 && gearset->ClassJob == cj.ClassJobParent.RowId) backup = gearset->Id;
        }

        return backup;
    }
}
