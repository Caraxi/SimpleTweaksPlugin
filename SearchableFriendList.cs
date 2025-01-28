using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin;

[TweakName("Searchable Friend List")]
[TweakDescription("Adds a search bar to the friend list.")]
[TweakAutoConfig]
[TweakReleaseVersion(UnreleasedVersion)]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
public unsafe class SearchableFriendList : Tweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Ignore selected group")]
        public bool IgnoreSelectedGroup;

        [TweakConfigOption("CTRL-F to focus search")]
        public bool SearchHotkey;
    }

    protected void DrawConfig() {
        if (ImGui.InputText("Search", ref searchString, 60)) {
            InfoProxyFriendList.Instance()->ApplyFilters();
        }
    }

    [TweakConfig] public Configs TweakConfig { get; private set; }

    [TweakHook(typeof(InfoProxyCommonList), nameof(InfoProxyCommonList.ApplyFilters), nameof(ApplyFiltersDetour))]
    private HookWrapper<InfoProxyCommonList.Delegates.ApplyFilters> applyFiltersHook;
    

    // Temp fix for ExtraFlags being in wrong offset.
    [StructLayout(LayoutKind.Explicit, Size = InfoProxyCommonList.CharacterData.StructSize)]
    private struct CharacterData2 {
#if DEBUG
#warning Remove this when Client Structs is updated in dalamud.
#endif
        
        [FieldOffset(0x00)] public InfoProxyCommonList.CharacterData CharacterData;
        [FieldOffset(0x20)] public uint ExtraFlags;
        public InfoProxyCommonList.DisplayGroup Group => (InfoProxyCommonList.DisplayGroup)(ExtraFlags >> 16);
        public string NameString => CharacterData.NameString;
        public ulong ContentId => CharacterData.ContentId;
    }

    private string searchString = string.Empty;

    private bool MatchesSearch(string name) {
        if (string.IsNullOrWhiteSpace(searchString)) return true;
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (searchString.StartsWith('^')) return name.StartsWith(searchString[1..], StringComparison.InvariantCultureIgnoreCase);
        if (searchString.EndsWith('$')) return name.EndsWith(searchString[..^1], StringComparison.InvariantCultureIgnoreCase);
        return name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase);
    }

    private void ApplyFiltersDetour(InfoProxyCommonList* infoProxyCommonList) {
        if (infoProxyCommonList != InfoProxyFriendList.Instance() || string.IsNullOrWhiteSpace(searchString)) {
            applyFiltersHook.Original(infoProxyCommonList);
            return;
        }

        var friendList = (InfoProxyFriendList*)infoProxyCommonList;
        var resets = new Dictionary<ulong, uint>();
        var resetFilterGroup = friendList->FilterGroup;

        try {
            friendList->FilterGroup = InfoProxyCommonList.DisplayGroup.None;
            var entryCount = friendList->GetEntryCount();

            SimpleLog.Verbose($"Applying Filters for {entryCount} friends.");

            for (var i = 0U; i < entryCount; i++) {
                var entry = (CharacterData2*)friendList->GetEntry(i);
                if (entry == null) continue;
                resets.Add(entry->ContentId, entry->ExtraFlags);
                if ((TweakConfig.IgnoreSelectedGroup || entry->Group == resetFilterGroup) && MatchesSearch(entry->NameString)) {
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
                var entry = (CharacterData2*)friendList->GetEntryByContentId(r.Key);
                entry->ExtraFlags = r.Value;
                SimpleLog.Verbose($"Reset {entry->NameString} group to {entry->Group}");
            }
        }
    }

    protected override void Enable() {
        PluginInterface.UiBuilder.Draw += DrawSearchUi;
    }

    private void DrawSearchUi() {
        var flAddon = Common.GetUnitBase("FriendList");
        var socialAddon = Common.GetUnitBase("Social");

        if (socialAddon == null || flAddon == null || flAddon->IsVisible == false) {
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
        ImGui.SetNextWindowPos(new Vector2(flAddon->X, flAddon->Y) + new Vector2(flAddon->GetScaledWidth(true) * 0.05f, flAddon->GetScaledHeight(true) * 0.90f));
        ImGui.SetNextWindowSize(new Vector2(flAddon->GetScaledWidth(true) * 0.5f, flAddon->GetScaledHeight(true) * 0.05f));
        if (ImGui.Begin("Friend Search Window", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground)) { 
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (Service.KeyState[VirtualKey.CONTROL] && Service.KeyState[VirtualKey.F]) {
                ImGui.SetKeyboardFocusHere();
            }

            if (ImGui.InputTextWithHint("##friendSearch", "Search...", ref searchString, 32)) {
                ReFilter();
            }
        }

        ImGui.End();
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
