using System;
using System.Collections.Concurrent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SimpleTweaksPlugin.Utility; 

public static unsafe class TooltipManager {

    private static SimpleEvent _event;
    private static ConcurrentDictionary<string, ConcurrentDictionary<uint, Func<string>>> _tooltips = new();
    
    static TooltipManager() {
        _event = new SimpleEvent(TooltipDelegate);
    }


    public static void AddTooltip(AtkUnitBase* atkUnitBase, AtkResNode* node, string tooltip) => AddTooltip(atkUnitBase, node, () => tooltip);
    public static void AddTooltip(AtkUnitBase* atkUnitBase, AtkResNode* node, Func<string> tooltip) {
        if (atkUnitBase == null || node == null) return;
        var addonName = Common.ReadString(atkUnitBase->Name, 0x20);
        if (!_tooltips.TryGetValue(addonName, out var addonDict)) {
            addonDict = new ConcurrentDictionary<uint, Func<string>>();
            if (!_tooltips.TryAdd(addonName, addonDict)) return;
        }
        addonDict.AddOrUpdate(node->NodeID, _ => tooltip, (_, _) => tooltip);

        _event.Remove(atkUnitBase, node, AtkEventType.MouseOver);
        _event.Remove(atkUnitBase, node, AtkEventType.MouseOut);
        _event.Add(atkUnitBase, node, AtkEventType.MouseOver);
        _event.Add(atkUnitBase, node, AtkEventType.MouseOut);
        node->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
        atkUnitBase->UpdateCollisionNodeList(false);
    }

    public static void RemoveTooltip(AtkUnitBase* atkUnitBase, AtkResNode* node) {
        if (atkUnitBase == null || node == null) return;
        _event.Remove(atkUnitBase, node, AtkEventType.MouseOver);
        _event.Remove(atkUnitBase, node, AtkEventType.MouseOut);
        var addonName = Common.ReadString(atkUnitBase->Name, 0x20);
        if (!_tooltips.TryGetValue(addonName, out var addonDict)) return;
        addonDict.TryRemove(node->NodeID, out _);
    }

    private static void TooltipDelegate(AtkEventType eventType, AtkUnitBase* atkUnitBase, AtkResNode* node) {
        var addonName = Common.ReadString(atkUnitBase->Name, 0x20);
        if (!_tooltips.TryGetValue(addonName, out var addonDict)) return;
        if (!addonDict.TryGetValue(node->NodeID, out Func<string> tooltip)) return;
        if (eventType == AtkEventType.MouseOver) {
            AtkStage.GetSingleton()->TooltipManager.ShowTooltip(atkUnitBase->ID, node, tooltip());
        } else if (eventType == AtkEventType.MouseOut) {
            AtkStage.GetSingleton()->TooltipManager.HideTooltip(atkUnitBase->ID);
        }
    }

    internal static void Destroy() {
        _event?.Dispose();
        _tooltips.Clear();
    }
    
}
