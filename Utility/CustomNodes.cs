using System.Collections.Generic;
using System.Text;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Utility; 

public static class CustomNodes {

    private static readonly Dictionary<string, uint> NodeIds = new();
    private static readonly Dictionary<uint, string> NodeNames = new();
    private static uint _nextId = 0x53541000;

    public static uint Get(BaseTweak tweak, string label = "", int index = 0) {
        return string.IsNullOrEmpty(label) ? Get($"{tweak.GetType().Name}", index) : Get($"{tweak.GetType().Name}::{label}", index);
    }
    
    public static uint Get(string name, int index = 0) {
        if (TryGet(name, index, out var id)) return id;
        lock (NodeIds) {
            lock (NodeNames) {
                id = _nextId;
                _nextId += 16;
                NodeIds.Add($"{name}#{index}", id);
                NodeNames.Add(id, $"{name}#{index}");
                return id;
            }
        }
    }

    public static bool TryGet(string name, out uint id) => TryGet(name, 0, out id);
    public static bool TryGet(string name, int index, out uint id) => NodeIds.TryGetValue($"{name}#{index}", out id);
    public static bool TryGet(uint id, out string name) => NodeNames.TryGetValue(id, out name);
    
    public const int
        TargetHP =             SimpleTweaksNodeBase + 1,
        SlideCastMarker =      SimpleTweaksNodeBase + 2,
        TimeUntilGpMax =       SimpleTweaksNodeBase + 3,
        ComboTimer =           SimpleTweaksNodeBase + 4,
        PartyListStatusTimer = SimpleTweaksNodeBase + 5,
        InventoryGil         = SimpleTweaksNodeBase + 6,
        GearPositionsBg      = SimpleTweaksNodeBase + 7, // and 8
        ClassicSlideCast =     SimpleTweaksNodeBase + 9,
        PaintingPreview =      SimpleTweaksNodeBase + 10,
        AdditionalInfo =       SimpleTweaksNodeBase + 11,
        CraftingGhostBar =     SimpleTweaksNodeBase + 12,
        CraftingGhostText =    SimpleTweaksNodeBase + 13,
        GlamNameReplacementText = SimpleTweaksNodeBase + 14,
        SimpleTweaksNodeBase = 0x53540000;
}