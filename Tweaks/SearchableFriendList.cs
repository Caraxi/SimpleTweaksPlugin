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
        PluginInterface.UiBuilder.Draw += DrawSearchUi;
    }

    private void DrawSearchUi() {
        var flAddon = Common.GetUnitBase<AddonFriendList>("FriendList");
        var socialAddon = Common.GetUnitBase("Social");

        if (socialAddon == null || flAddon == null || flAddon->IsVisible == false || flAddon->AddButton == null || flAddon->AddButton->OwnerNode == null) {
            if (string.IsNullOrWhiteSpace(searchString)) return;
            searchString = string.Empty;
            ReFilter();
            return;
        }

        var focusedList = &RaptureAtkUnitManager.Instance()->FocusedUnitsList;
        var isFocused = false;
        foreach (var f in focusedList->Entries) {
            if (f.Value == null) continue;
            if (f.Value != flAddon && f.Value != socialAddon) continue;
            isFocused = true;
            break;
        }

        if (!isFocused) return;

        ImGui.SetNextWindowViewport(ImGuiHelpers.MainViewport.ID);

        var pos = (new Vector2(flAddon->X, flAddon->Y) + new Vector2(flAddon->AddButton->OwnerNode->X, flAddon->AddButton->OwnerNode->Y) * flAddon->Scale);
        var size = new Vector2(flAddon->AddButton->OwnerNode->GetWidth(), flAddon->AddButton->OwnerNode->GetHeight()) * flAddon->Scale;
        
        if (flAddon->AddButton->OwnerNode->IsVisible()) {
            // I have no idea if or when this is ever visible... but move over just incase
            pos += size * Vector2.UnitX;
        }
        
        ImGui.SetNextWindowPos(pos + ImGui.GetMainViewport().Pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero)) {
            if (ImGui.Begin("Friend Search Window", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground)) {
                ImGui.SetWindowFontScale(1f / ImGuiHelpers.GlobalScale * flAddon->Scale);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (TweakConfig.SearchHotkey && Service.KeyState[VirtualKey.CONTROL] && Service.KeyState[VirtualKey.F]) {
                    ImGui.SetKeyboardFocusHere();
                }
                
                using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, flAddon->Scale < 1 ? 1 : 2))
                using (ImRaii.PushColor(ImGuiCol.FrameBg, 0)) 
                using (ImRaii.PushColor(ImGuiCol.Border, searchString.IsNullOrWhitespace() ? 0x80808080 : 0xFF11AAFF))
                using (ImRaii.PushColor(ImGuiCol.BorderShadow, searchString.IsNullOrWhitespace() ? 0 : 0x800000FF)) {
                    if (ImGui.InputTextWithHint("##friendSearch", "Search...", ref searchString, 32)) {
                        ReFilter();
                    }
                }
                
                ImGui.SetWindowFontScale(1);
            }
            
            ImGui.End();
        }
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= DrawSearchUi;
    }

    protected override void AfterDisable() {
        ReFilter();
    }
    
    [AddonPreRequestedUpdate("FriendList"), AddonPostRequestedUpdate("FriendList")]
    private void ReFilter() {
        InfoProxyFriendList.Instance()->ApplyFilters();
    }
}
