using System;
using System.Collections.Generic;
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

        { 23, 48 }, // BRD
        { 31, 50 }, // MCH
        { 38, 52 }, // DNC

        { 25, 58 }, // BLM
        { 27, 60 }, // SMN
        { 35, 62 }, // RDM
        { 42, 64 }, // PCT
        { 36, 66 }, // BLU

        { 08, 71 }, // CRP
        { 09, 72 }, // BSM
        { 10, 73 }, // ARM
        { 11, 74 }, // GSM
        { 12, 75 }, // LTW
        { 13, 76 }, // WVR
        { 14, 77 }, // ALC
        { 15, 78 }, // CUL

        { 16, 84 }, // MIN
        { 17, 86 }, // BTN
        { 18, 88 }, // FSH
    };

    [TweakHook] private HookWrapper<AtkUnitBase.Delegates.ReceiveEvent> eventHook;

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

            var dohIconImage = atkUnitBase->GetImageNodeById(68);
            if (dohIconImage != null) {
                dohIconImage->AtkResNode.NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse;
                dohIconImage->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*)atkUnitBase, (AtkResNode*)dohIconImage, false);
            }

            var dohHeaderText = atkUnitBase->GetTextNodeById(69);
            if (dohHeaderText != null) {
                dohHeaderText->AtkResNode.NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse;
                dohHeaderText->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*)atkUnitBase, (AtkResNode*)dohIconImage, false);
            }

            SimpleLog.Log("CharacterClass Events Setup");
        }
    }

    //private void OnEvent(AtkUnitBase* atkUnitBase, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
    [AddonPreReceiveEvent("CharacterClass")]
    private void EventHandle(AddonReceiveEventArgs args) {
        var eventType = (AtkEventType)args.AtkEventType;
        var eventParam = args.EventParam;

        if (eventType == AtkEventType.MouseClick && eventParam == 0x53542000) {
            // Open Desynthesis Skill Window
            args.AtkEventType = (byte)AtkEventType.ButtonClick;
            args.EventParam = 22;
            return;
        }

        if (eventType == AtkEventType.InputReceived) {
            var a5 = (byte*)args.Data;
            if (a5 == null || a5[0] != 0x01 || a5[4] != 1) {
                return;
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
