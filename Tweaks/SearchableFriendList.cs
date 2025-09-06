using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Searchable Friend List")]
[TweakDescription("Adds a search bar to the friend list.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.10.7.0")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
public unsafe class SearchableFriendList : Tweak {
    private TextInputNode searchInput;
    private TextNode searchHint;
    
    public class Configs : TweakConfig {
        [TweakConfigOption("Ignore selected filter group")]
        public bool IgnoreSelectedGroup;

        [TweakConfigOption("CTRL-F to focus search")]
        public bool SearchHotkey = true;
    }

    protected void DrawConfig() {
        if (ImGui.InputText("Search", ref searchString, 60)) {
            InfoProxyFriendList.Instance()->ApplyFilters();
        }
    }

    [TweakConfig] public Configs TweakConfig { get; private set; }

    [TweakHook(typeof(InfoProxyCommonList), nameof(InfoProxyCommonList.ApplyFilters), nameof(ApplyFiltersDetour))]
    private HookWrapper<InfoProxyCommonList.Delegates.ApplyFilters> applyFiltersHook;

    private string searchString = string.Empty;

    private bool MatchesSearch(string name) {
        if (string.IsNullOrWhiteSpace(searchString)) return true;
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (searchString.StartsWith('^')) return name.StartsWith(searchString[1..], StringComparison.InvariantCultureIgnoreCase);
        if (searchString.EndsWith('$')) return name.EndsWith(searchString[..^1], StringComparison.InvariantCultureIgnoreCase);
        return name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase);
    }

    private void ApplyFiltersDetour(InfoProxyCommonList* infoProxyCommonList) {
        if (infoProxyCommonList != InfoProxyFriendList.Instance()) {
            applyFiltersHook.Original(infoProxyCommonList);
            return;
        }

        if (string.IsNullOrWhiteSpace(searchString)) {
            using var noSearchPerformanceRun = PerformanceMonitor.Run("SearchableFriendList.ApplyFilters.NoSearch");
            applyFiltersHook.Original(infoProxyCommonList);
            return;
        }

        using var performanceRun = PerformanceMonitor.Run("SearchableFriendList.ApplyFilters.WithSearch");

        var friendList = (InfoProxyFriendList*)infoProxyCommonList;
        var resets = new Dictionary<ulong, uint>();
        var resetFilterGroup = friendList->FilterGroup;

        try {
            friendList->FilterGroup = InfoProxyCommonList.DisplayGroup.None;
            var entryCount = friendList->GetEntryCount();

            SimpleLog.Verbose($"Applying Filters for {entryCount} friends.");

            for (var i = 0U; i < entryCount; i++) {
                var entry = friendList->GetEntry(i);
                if (entry == null) continue;
                resets.Add(entry->ContentId, entry->ExtraFlags);
                if ((TweakConfig.IgnoreSelectedGroup || resetFilterGroup == InfoProxyCommonList.DisplayGroup.All || entry->Group == resetFilterGroup) && MatchesSearch(entry->NameString)) {
                    SimpleLog.Verbose($"{entry->NameString} contains {searchString}. Group is {entry->Group}");
                    entry->ExtraFlags &= 0xFFFF;
                    SimpleLog.Verbose($"- Group is changed to {entry->Group}");
                } else {
                    SimpleLog.Verbose($"{entry->NameString} does not contain {searchString}. Group is {entry->Group}");
                    entry->ExtraFlags = (entry->ExtraFlags & 0xFFFF) | ((uint)(1 & 0xFF) << 16);
                }
            }
        } finally {
            applyFiltersHook.Original(infoProxyCommonList);
            friendList->FilterGroup = resetFilterGroup;
            foreach (var r in resets) {
                var entry = friendList->GetEntryByContentId(r.Key);
                entry->ExtraFlags = r.Value;
                SimpleLog.Verbose($"Reset {entry->NameString} group to {entry->Group}");
            }
        }
    }

    protected override void Enable() {
        searchHint = new TextNode() {
            IsVisible = true, 
            Position = new Vector2(10, 6), 
            String = "Search..."
        };
        
        searchInput = new TextInputNode() {
            IsVisible = true, 
            OnInputReceived = (str) => {
                searchString = str.TextValue;
                ReFilter();
            },
            OnFocused = () => searchHint.IsVisible = false, 
            OnUnfocused = () => searchHint.IsVisible = string.IsNullOrWhiteSpace(searchInput.String)
        };


        Common.NativeController.AttachNode(searchHint, searchInput);
        
        if (Common.GetUnitBase(out AddonFriendList* friendList, "FriendList")) {
            SetupFiendList(friendList);
        }
        
        // PluginInterface.UiBuilder.Draw += DrawSearchUi;
    }

    [AddonPostSetup("FriendList")]
    private void SetupFiendList(AddonFriendList* friendList) {
        searchString = string.Empty;
        searchInput.Position = new Vector2(5,  500);
        searchInput.Size = new Vector2(300,  28);
        searchInput.String = searchString;
        Common.NativeController.AttachNode(searchInput, friendList->RootNode);
        Common.FrameworkUpdate += FrameworkUpdate;
    }

    [AddonFinalize("FriendList")]
    private void FinalizeFriendList(AddonFriendList* friendList) {
        Common.NativeController.DetachNode(searchInput);
        Common.FrameworkUpdate -= FrameworkUpdate;
    }

    private void FrameworkUpdate() {
        if (!Common.GetUnitBase("FriendList", out var flAddon)) return;
        if (!Common.GetUnitBase("Social", out var socialAddon)) return;
        if (!TweakConfig.SearchHotkey || !Service.KeyState[VirtualKey.CONTROL] || !Service.KeyState[VirtualKey.F]) return;
        if (!Common.AnyFocused(flAddon, socialAddon)) return;
        Service.KeyState[VirtualKey.F] = false;
        flAddon->SetFocusNode(&searchInput.CollisionNode.Node->AtkResNode);
    }

    protected override void Disable() {
        searchHint?.Dispose();
        searchInput?.Dispose();
    }

    protected override void AfterDisable() {
        ReFilter();
    }
    
    [AddonPreRequestedUpdate("FriendList"), AddonPostRequestedUpdate("FriendList")]
    private void ReFilter() {
        InfoProxyFriendList.Instance()->ApplyFilters();
    }
}
