using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class CharacterClassSwitcher : Tweak {
    public override string Name => "Character Window Job Switcher";
    public override string Description => "Allow clicking on classes to switch to gearsets.";

    private delegate byte EventHandle(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, byte* a5);
    private delegate void* SetupHandle(AtkUnitBase* atkUnitBase, int a2);
    private HookWrapper<EventHandle> eventHook;
    private HookWrapper<SetupHandle> setupHook;

    public override void Enable() {
        eventHook ??= Common.Hook<EventHandle>("48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 4D 8B D1", EventDetour);
        eventHook.Enable();
        setupHook ??= Common.Hook<SetupHandle>("48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 41 56 41 57 48 8D 68 A1 48 81 EC ?? ?? ?? ?? 0F 29 70 C8 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 17 F3 0F 10 35 ?? ?? ?? ?? 45 33 C9 45 33 C0 F3 0F 11 74 24 ?? 0F 57 C9 48 8B F9", SetupDetour);
        setupHook.Enable();
        try {
            SetupCharacterClass(Common.GetUnitBase("CharacterClass"));
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        base.Enable();
    }

    private Dictionary<uint, uint> ClassJobComponentMap = new() {
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

        { 23, 46 }, // BRD
        { 31, 48 }, // MCH
        { 38, 50 }, // DNC

        { 25, 56 }, // BLM
        { 27, 58 }, // SMN
        { 35, 60 }, // RDM
        { 36, 62 }, // BLU

        { 08, 67 }, // CRP
        { 09, 68 }, // BSM
        { 10, 69 }, // ARM
        { 11, 70 }, // GSM
        { 12, 71 }, // LTW
        { 13, 72 }, // WVR
        { 14, 73 }, // ALC
        { 15, 74 }, // CUL
        
        { 16, 80 }, // MIN
        { 17, 82 }, // BTN
        { 18, 84 }, // FSH
    };


    private void SetupCharacterClass(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase != null) {
            SimpleLog.Log("Setup CharacterClass Events");
            foreach (var (cjId, nodeId) in ClassJobComponentMap) {
                var componentNode = (AtkComponentNode*) atkUnitBase->GetNodeById(nodeId);
                if (componentNode == null) continue;

                switch (componentNode->AtkResNode.Type) {
                    case (NodeType)1001: {
                        // DoH
                        var evt = componentNode->AtkResNode.AtkEventManager.Event;
                        while (evt != null && evt->Param == 14 + cjId) {
                            if (evt->Type == (byte)AtkEventType.ButtonClick) evt->Param = 0x53541000 + cjId;
                            evt = evt->NextEvent;
                        }
                        
                        break;
                    }
                    case (NodeType)1003: {
                        // Others
                        var colNode = (AtkCollisionNode*)componentNode->Component->UldManager.SearchNodeById(8);
                        if (colNode == null) continue;
                        if (colNode->AtkResNode.Type != NodeType.Collision) continue;
                        colNode->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, (AtkResNode*) colNode, false);
                        colNode->AtkResNode.AddEvent(AtkEventType.InputReceived, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, (AtkResNode*) colNode, false);
                        break;
                    }
                }
            }

            var dohIconImage = atkUnitBase->GetImageNodeById(64);
            if (dohIconImage != null) {
                dohIconImage->AtkResNode.Flags |= (short)(NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse);
                dohIconImage->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*) atkUnitBase, (AtkResNode*) dohIconImage, false);
            }
            var dohHeaderText = atkUnitBase->GetTextNodeById(65);
            if (dohHeaderText != null) {
                dohHeaderText->AtkResNode.Flags |= (short)(NodeFlags.EmitsEvents | NodeFlags.HasCollision | NodeFlags.RespondToMouse);
                dohHeaderText->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53542000, (AtkEventListener*) atkUnitBase, (AtkResNode*) dohIconImage, false);
            }
            
            SimpleLog.Log("CharacterClass Events Setup");
        }
    }

    private void* SetupDetour(AtkUnitBase* atkUnitBase, int a2) {
        var retVal = setupHook.Original(atkUnitBase, a2);
        try {
            SetupCharacterClass(atkUnitBase);
        } catch {
            //
        }
        return retVal;
    }

    private byte EventDetour(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkEvent* atkEvent, byte* a5) {
        if (eventType == AtkEventType.MouseClick && eventParam == 0x53542000) // Open Desynthesis Skill Window
            return eventHook.Original(atkUnitBase, AtkEventType.ButtonClick, 22, atkEvent, a5);
        if (eventType == AtkEventType.InputReceived) {
            if (a5 == null || a5[0] != 0x01 || a5[4] != 1) return eventHook.Original(atkUnitBase, eventType, eventParam, atkEvent, a5);
        }
        try {
            if (eventType is AtkEventType.MouseClick or AtkEventType.ButtonClick or AtkEventType.InputReceived && (eventParam & 0x53541000) == 0x53541000) {
                var cjId = eventParam - 0x53541000;
                var classJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(cjId);
                SimpleLog.Log($"Change Class: ClassJob#{cjId} ({classJob.Abbreviation.RawString})");
                
                if (classJob != null) {
                    var gearsetId = GetGearsetForClassJob(classJob);
                    if (gearsetId != null) {
                        SimpleLog.Log($"Send Command: /gearset change {gearsetId.Value + 1}");
                        ChatHelper.SendMessage($"/gearset change {gearsetId.Value + 1}");
                    } else {
                        Service.Chat.PrintError($"No saved gearset for {classJob.Name.RawString}");
                    }
                    return 1;
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return eventHook.Original(atkUnitBase, eventType, eventParam, atkEvent, a5);
    }

    private byte? GetGearsetForClassJob(ClassJob cj) {
        byte? backup = null;
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++) {
            var gearset = gearsetModule->Gearset[i];
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->ID != i) continue;
            if (gearset->ClassJob == cj.RowId) return gearset->ID;
            if (backup == null && cj.ClassJobParent.Row != 0 && gearset->ClassJob == cj.ClassJobParent.Row) backup = gearset->ID;
        }

        return backup;
    }

    public override void Disable() {
        eventHook?.Disable();
        setupHook?.Disable();

        var atkUnitBase = Common.GetUnitBase("CharacterClass");
        if (atkUnitBase != null) {
            try {
                foreach (var (cjId, nodeId) in ClassJobComponentMap) {
                    var componentNode = (AtkComponentNode*) atkUnitBase->GetNodeById(nodeId);
                    if (componentNode == null) continue;

                    switch (componentNode->AtkResNode.Type) {
                        case (NodeType)1001: {
                            var evt = componentNode->AtkResNode.AtkEventManager.Event;
                            while (evt != null) {
                                if (evt->Type == (byte)AtkEventType.ButtonClick && evt->Param == 0x53541000 + cjId) evt->Param = 14 + cjId;
                                evt = evt->NextEvent;
                            }
                            break;
                        }
                        case (NodeType)1003: {
                            var colNode = (AtkCollisionNode*)componentNode->Component->UldManager.SearchNodeById(8);
                            if (colNode == null) continue;
                            if (colNode->AtkResNode.Type != NodeType.Collision) continue;
                            colNode->AtkResNode.RemoveEvent(AtkEventType.MouseClick, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, false);
                            colNode->AtkResNode.RemoveEvent(AtkEventType.InputReceived, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, false);
                            break;
                        }
                    }
                }
            } catch {
                //
            }
        }

        base.Disable();
    }

    public override void Dispose() {
        setupHook?.Dispose();
        eventHook?.Dispose();
        base.Dispose();
    }
}