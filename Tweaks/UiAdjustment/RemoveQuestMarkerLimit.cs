using FFXIVClientStructs.FFXIV.Client.System.String;
using SimpleTweaksPlugin.Helper;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    // Unknown size and I really don't want to work it out.
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct Map {
        [FieldOffset(0x78)] public QuestMarkerArray QuestMarkers;

        [StructLayout(LayoutKind.Sequential, Size = 30 * 0x90)]
        public struct QuestMarkerArray {
            private fixed byte data[30 * 0x90];
            public MapMarkerInfo* this[int index] {
                get {
                    if (index < 0 || index > 30) {
                        return null;
                    }

                    fixed (byte* pointer = this.data) {
                        return (MapMarkerInfo*)(pointer + sizeof(MapMarkerInfo) * index);
                    }
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x90)]
    public struct MapMarkerInfo {
        [FieldOffset(0x8B)] public byte ShouldRender;
    }

    public unsafe class RemoveQuestMarkerLimit : UiAdjustments.SubTweak {
        public override string Name => "Remove Quest Marker Limit";
        public override string Description => "Allow the map and minimap to display markers for more than 5 active quests.";

        private delegate void* SetQuestMarkerInfoDelegate(Map* thisPtr, uint index, ushort questId, Utf8String* name, ushort recommendedLevel);
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

            map->QuestMarkers[(int)index]->ShouldRender = 1;

            return result;
        }
    }
}
