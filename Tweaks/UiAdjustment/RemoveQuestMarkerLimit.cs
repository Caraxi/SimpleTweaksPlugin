using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.Interop;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Remove Quest Marker Limit")]
[TweakDescription("Allow the map and minimap to display markers for more than 5 active quests.")]
[Changelog(UnreleasedVersion, "Fix tweak not working since API 9")]
public unsafe class RemoveQuestMarkerLimit : UiAdjustments.SubTweak {
    private delegate void* SetQuestMarkerInfoDelegate(Map* map, uint index, ushort questId, Utf8String* name, ushort recommendedLevel);
    [TweakHook, Signature("E8 ?? ?? ?? ?? 0F B6 5B 0A", DetourName = nameof(SetQuestMarkerInfoDetour))]
    private HookWrapper<SetQuestMarkerInfoDelegate> setQuestMarkerInfoHook;

    private void* SetQuestMarkerInfoDetour(Map* map, uint index, ushort questId, Utf8String* name, ushort recommendedLevel) {
        var result = setQuestMarkerInfoHook.Original(map, index, questId, name, recommendedLevel);
        if (!QuestManager.Instance()->NormalQuestsSpan[(int)index].IsHidden) {
            map->QuestDataSpan.GetPointer((int) index)->ShouldRender = true;
        }
        return result;
    }
}