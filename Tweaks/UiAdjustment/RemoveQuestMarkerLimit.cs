using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class RemoveQuestMarkerLimit : UiAdjustments.SubTweak {
    public override string Name => "Remove Quest Marker Limit";
    public override string Description => "Allow the map and minimap to display markers for more than 5 active quests.";

    private delegate void* SetQuestMarkerInfoDelegate(Map* map, uint index, ushort questId, Utf8String* name, ushort recommendedLevel);
    private HookWrapper<SetQuestMarkerInfoDelegate> setQuestMarkerInfoHook;

    public override void Enable() {
        this.setQuestMarkerInfoHook ??= Common.Hook<SetQuestMarkerInfoDelegate>(
            "E8 ?? ?? ?? ?? 0F B6 5B 0A",
            this.SetQuestMarkerInfoDetour
        );
        this.setQuestMarkerInfoHook?.Enable();

        base.Enable();
    }

    public override void Disable() {
        this.setQuestMarkerInfoHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        this.setQuestMarkerInfoHook?.Dispose();
        base.Dispose();
    }

    private void* SetQuestMarkerInfoDetour(Map* map, uint index, ushort questId, Utf8String* name, ushort recommendedLevel) {
        var result = this.setQuestMarkerInfoHook.Original(map, index, questId, name, recommendedLevel);
        if (!QuestManager.Instance()->Quest[(int)index]->IsHidden) {
            map->QuestMarkers[(int)index]->ShouldRender = 1;
        }

        return result;
    }
}