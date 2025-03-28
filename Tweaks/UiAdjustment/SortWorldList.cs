using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;


[TweakName("Sort World Visit List")]
[TweakDescription("Removes the randomization from the data center visit world list.")]
[TweakReleaseVersion(UnreleasedVersion)]
public unsafe class SortWorldList : Tweak {
    private class Entry(string name, AtkResNode* node) {
        public string Name => name;
        public AtkResNode* Node => node;
    }

    protected override void Enable() {
        if (Common.GetUnitBase("LobbyDKTWorldList", out var unitBase)) PostRefresh(unitBase);
    }

    [AddonPostRefresh("LobbyDKTWorldList")]
    private void PostRefresh(AtkUnitBase* addon) {
        var list = addon->GetComponentByNodeId(22);
        if (list == null) return;
        
        List<Entry> entries = [];
        for (var nodeIndex = 0U; nodeIndex < addon->AtkValuesSpan[1833].UInt; nodeIndex++) {
            var nodeId = nodeIndex == 0 ? 3U : 31000U + nodeIndex;
            var node = Common.GetNodeByID(list, nodeId);
            if (node == null || (ushort)node->Type < 1000 || node->Y == 0) continue;
            var component = node->GetComponent();
            if (component == null) continue;
            var textNode = (AtkTextNode*) component->GetTextNodeById(2);
            if (textNode == null) continue;
            var name = textNode->NodeText.StringPtr.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            entries.Add(new Entry(name, node));
        }

        var sortedIndex = 0;
        foreach (var e in entries.OrderBy(e => e.Name)) {
            e.Node->SetYFloat(24 * ++sortedIndex);
        }
    }
}
