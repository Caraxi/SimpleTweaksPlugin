using System;
using System.Collections.Generic;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Configuration;

namespace SimpleTweaksPlugin.Utility; 

public static unsafe class GameConfig {

    public class GameConfigSection {

        private readonly ConfigBase* configBase;
        
        private readonly Dictionary<string, uint> indexMap = new();

        public GameConfigSection(ConfigBase* configBase) {
            this.configBase = configBase;
            
            var e = configBase->ConfigEntry;
            for (var i = 0U; i < configBase->ConfigCount; i++, e++) {
                if (e->Name == null) continue;
                var eName = MemoryHelper.ReadStringNullTerminated(new IntPtr(e->Name));
                if (!indexMap.ContainsKey(eName)) indexMap.Add(eName, i);
            }
        }

        private bool TryGetIndex(string name, out uint index) {
            if (indexMap.TryGetValue(name, out index)) return true;
            var e = configBase->ConfigEntry;
            for (var i = 0U; i < configBase->ConfigCount; i++, e++) {
                if (e->Name == null) continue;
                var eName = MemoryHelper.ReadStringNullTerminated(new IntPtr(e->Name));
                if (eName == name) {
                    indexMap.Add(name, i);
                    index = i;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        private bool TryGetEntry(uint index, out ConfigEntry* entry) {
            entry = null;
            if (configBase->ConfigEntry == null || index >= configBase->ConfigCount) return false;
            entry = configBase->ConfigEntry;
            entry += index;
            return true;
        }
        
        public bool TryGetBool(string name, out bool value) {
            value = false;
            if (!TryGetIndex(name, out var index)) return false;
            if (!TryGetEntry(index, out var entry)) return false;
            value = entry->Value.UInt != 0;
            return true;
        }

        public bool GetBool(string name) {
            if (!TryGetBool(name, out var value)) throw new Exception($"Failed to get Bool '{name}'");
            return value;
        }

        public void Set(string name, bool value) {
            if (!TryGetIndex(name, out var index)) return;
            if (!TryGetEntry(index, out var entry)) return;
            
            entry->Value.UInt = value ? 1U : 0U;
        }
        
        public bool TryGetUInt(string name, out uint value) {
            value = 0;
            if (!TryGetIndex(name, out var index)) return false;
            if (!TryGetEntry(index, out var entry)) return false;
            value = entry->Value.UInt;
            return true;
        }

        public uint GetUInt(string name) {
            if (!TryGetUInt(name, out var value)) throw new Exception($"Failed to get UInt '{name}'");
            return value;
        }

        public void Set(string name, uint value) {
            if (!TryGetIndex(name, out var index)) return;
            if (!TryGetEntry(index, out var entry)) return;
            entry->Value.UInt = value;
        }
        
        public bool TryGetFloat(string name, out float value) {
            value = 0;
            if (!TryGetIndex(name, out var index)) return false;
            if (!TryGetEntry(index, out var entry)) return false;
            value = entry->Value.Float;
            return true;
        }

        public float GetFloat(string name) {
            if (!TryGetFloat(name, out var value)) throw new Exception($"Failed to get Float '{name}'");
            return value;
        }

        public void Set(string name, float value) {
            if (!TryGetIndex(name, out var index)) return;
            if (!TryGetEntry(index, out var entry)) return;
            entry->Value.Float = value;
        }
    }

    static GameConfig() {
        System = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.ConfigBase);
        UiConfig = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiConfig);
        UiControl = new GameConfigSection(&Framework.Instance()->SystemConfig.CommonSystemConfig.UiControlConfig);
    }


    public static GameConfigSection System;
    public static GameConfigSection UiConfig;
    public static GameConfigSection UiControl;
}

