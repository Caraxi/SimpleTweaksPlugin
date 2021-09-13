using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace SimpleTweaksPlugin.Helper {
    public static class Extensions {
        public static string GetDescription(this Enum @enum) {
            var eType = @enum.GetType();
            var member = eType.GetMember(@enum.ToString());
            if ((member.Length <= 0)) return @enum.ToString();
            var attribs = member[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            return attribs.Length > 0 ? ((System.ComponentModel.DescriptionAttribute)attribs[0]).Description : @enum.ToString();
        }

        public static unsafe string GetString(this Utf8String utf8String) {
            var s = utf8String.BufUsed > int.MaxValue ? int.MaxValue : (int) utf8String.BufUsed;
            try {
                return s <= 1 ? string.Empty : Encoding.UTF8.GetString(utf8String.StringPtr, s - 1);
            } catch (Exception ex) {
                return $"<<{ex.Message}>>";
            }
        }

        public static SeString GetSeString(this Utf8String utf8String) {
            return SimpleTweaksPlugin.Plugin.Common.ReadSeString(utf8String);
        }

        public static void SetSeString(this Utf8String utf8String, SeString seString) {
            SimpleTweaksPlugin.Plugin.Common.WriteSeString(utf8String, seString);
        }

        private static readonly Dictionary<VirtualKey, string> NamedKeys = new() {
            { VirtualKey.KEY_0, "0"},
            { VirtualKey.KEY_1, "1"},
            { VirtualKey.KEY_2, "2"},
            { VirtualKey.KEY_3, "3"},
            { VirtualKey.KEY_4, "4"},
            { VirtualKey.KEY_5, "5"},
            { VirtualKey.KEY_6, "6"},
            { VirtualKey.KEY_7, "7"},
            { VirtualKey.KEY_8, "8"},
            { VirtualKey.KEY_9, "9"},
            { VirtualKey.CONTROL, "Ctrl"},
            { VirtualKey.MENU, "Alt"},
            { VirtualKey.SHIFT, "Shift"},
        };
        public static string GetKeyName(this VirtualKey k) => NamedKeys.ContainsKey(k) ? NamedKeys[k] : k.ToString();
    }
}
