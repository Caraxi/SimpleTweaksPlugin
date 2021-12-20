using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

// TODO:
// - Work out a way to function on DoH

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class CharacterClassSwitcher : Tweak {
        public override string Name => "Character Window Job Switcher";
        public override string Description => "Allow clicking on classes to switch to gearsets. [Note: does not work on crafters]";

        public override bool Experimental => true;

        private delegate byte EventHandle(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, void* a4, void* a5);
        private delegate void* SetupHandle(AtkUnitBase* atkUnitBase, int a2);
        private HookWrapper<EventHandle> eventHook;
        private HookWrapper<SetupHandle> setupHook;

        public override void Enable() {
            eventHook ??= Common.Hook<EventHandle>("48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 4D 8B D1", EventDetour);
            eventHook.Enable();
            setupHook ??= Common.Hook<SetupHandle>("48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 41 56 41 57 48 8D 68 A1 48 81 EC ?? ?? ?? ?? 0F 29 70 C8 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 17 F3 0F 10 35 ?? ?? ?? ?? 45 33 C9 45 33 C0 F3 0F 11 74 24 ?? 0F 57 C9 48 8B F9", SetupDetour);
            setupHook.Enable();
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

            { 16, 80 }, // MIN
            { 17, 82 }, // BTN
            { 18, 84 }, // FSH
        };


        private void* SetupDetour(AtkUnitBase* atkUnitBase, int a2) {
            var retVal = setupHook.Original(atkUnitBase, a2);
            if (atkUnitBase != null) {
                SimpleLog.Log("Setup CharacterClass Events");
                try {
                    foreach (var (cjId, nodeId) in ClassJobComponentMap) {
                        var componentNode = (AtkComponentNode*) atkUnitBase->GetNodeById(nodeId);
                        if (componentNode == null) continue;
                        if (componentNode->AtkResNode.Type != (NodeType)1003) continue;
                        var colNode = (AtkCollisionNode*)componentNode->Component->UldManager.SearchNodeById(8);
                        if (colNode == null) continue;
                        if (colNode->AtkResNode.Type != NodeType.Collision) continue;
                        colNode->AtkResNode.AddEvent(AtkEventType.MouseClick, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, null, false);
                    }
                    SimpleLog.Log("CharacterClass Events Setup");
                } catch {
                    //
                }
            }
            return retVal;
        }

        private byte EventDetour(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, void* a4, void* a5) {
            try {
                if (eventType == AtkEventType.MouseClick && (eventParam & 0x53541000) == 0x53541000) {
                    var cjId = eventParam - 0x53541000;
                    SimpleLog.Log($"Change Class: ClassJob#{cjId}");
                    var classJob = Service.Data.Excel.GetSheet<ClassJob>()?.GetRow(cjId);
                    if (classJob != null) {
                        SimpleLog.Log($"Send Command: /gearset change {classJob.Abbreviation.RawString}");
                        Plugin.XivCommon.Functions.Chat.SendMessage($"/gearset change {classJob.Abbreviation.RawString}");
                        return 1;
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }

            return eventHook.Original(atkUnitBase, eventType, eventParam, a4, a5);
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
                        if (componentNode->AtkResNode.Type != (NodeType)1003) continue;
                        var colNode = (AtkCollisionNode*)componentNode->Component->UldManager.SearchNodeById(8);
                        if (colNode == null) continue;
                        if (colNode->AtkResNode.Type != NodeType.Collision) continue;
                        colNode->AtkResNode.RemoveEvent(AtkEventType.MouseClick, 0x53541000 + cjId, (AtkEventListener*) atkUnitBase, false);
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
}

