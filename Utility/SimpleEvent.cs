using System;
using System.Collections.Generic;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Utility; 

public unsafe class SimpleEvent : IDisposable {
    public delegate void SimpleEventDelegate(AtkEventType eventType, AtkUnitBase* atkUnitBase, AtkResNode* node);
    
    public SimpleEventDelegate Action { get; }
    public uint ParamKey { get; }
    
    public SimpleEvent(SimpleEventDelegate action) {
        var newParam = 0x53540000u;
        while (EventHandlers.ContainsKey(newParam)) {
            if (++newParam >= 0x53550000u) throw new Exception("Too many event handlers...");
        }
        
        this.ParamKey = newParam;
        this.Action = action;

        EventHandlers.Add(newParam, this);
    }
    
    public void Dispose() {
        if (EventHandlers.ContainsKey(ParamKey)) {
            EventHandlers.Remove(ParamKey);
        }
    }

    public void Add(AtkUnitBase* unitBase, AtkResNode* node, AtkEventType eventType) {
        node->AddEvent(eventType, ParamKey, (AtkEventListener*) unitBase, node, true);
    }

    public void Remove(AtkUnitBase* unitBase, AtkResNode* node, AtkEventType eventType) {
        node->RemoveEvent(eventType, ParamKey, (AtkEventListener*) unitBase, true);
    }
    
    // Statics
    
    private delegate void* GlobalEventDelegate(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkResNode** eventData, uint* a5);
    private static readonly HookWrapper<GlobalEventDelegate> GlobalEventHook;
    
    static SimpleEvent() {
        GlobalEventHook = Common.Hook<GlobalEventDelegate>("48 89 5C 24 ?? 48 89 7C 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 50 44 0F B7 F2", GlobalEventDetour);
        GlobalEventHook?.Enable();
    }
    
    private static void* GlobalEventDetour(AtkUnitBase* atkUnitBase, AtkEventType eventType, uint eventParam, AtkResNode** eventData, uint* a5) {
        if (EventHandlers.ContainsKey(eventParam)) {
            SimpleLog.Debug($"Simple Event #{eventParam:X} [{eventType}] on {MemoryHelper.ReadString(new IntPtr(atkUnitBase->Name), 0x20)} ({(ulong)eventData[0]:X})");
            try {
                EventHandlers[eventParam].Action(eventType, atkUnitBase, eventData[0]);
                return null;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        return GlobalEventHook.Original(atkUnitBase, eventType, eventParam, eventData, a5);
    }

    internal static void Destroy() {
        GlobalEventHook?.Disable();
        GlobalEventHook?.Dispose();
    }
    
    private static readonly Dictionary<uint, SimpleEvent> EventHandlers = new();
}

