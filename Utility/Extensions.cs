using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Gui.ContextMenus;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;
using TextPayload = Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload;

namespace SimpleTweaksPlugin.Utility; 

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
        return Common.ReadSeString(utf8String);
    }

    public static void SetSeString(this Utf8String utf8String, SeString seString) {
        Common.WriteSeString(utf8String, seString);
    }

    public static int GetStableHashCode(this string str)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for(int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i+1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i+1];
            }

            return hash1 + (hash2*1566083941);
        }
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
    public static bool Cutscene(this Condition condition) => condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.OccupiedInCutSceneEvent];
    public static bool Duty(this Condition condition) => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95] || condition[ConditionFlag.BoundToDuty97];
    public static string GetKeyName(this VirtualKey k) => NamedKeys.ContainsKey(k) ? NamedKeys[k] : k.ToString();
    
    
    /// <summary>
    /// Adds a new context menu item, and removes the [D] from the name.
    /// </summary>
public static void AddSimpleItem(this ContextMenuOpenedArgs args, SeString name, CustomContextMenuItemSelectedDelegate action) {
    args.AddCustomItem(name, action);
    try {
        var itemList = args.GetItems();
        if (itemList == null || itemList.Count < 1) return;
        var lastItem = itemList[^1];
        if (lastItem.Name.Payloads.Count > 3 && lastItem.Name.Payloads[1] is TextPayload tpl) {
            tpl.Text = $"{(char)SeIconChar.ServerTimeEn} ";
        }
    } catch (Exception ex) {
        SimpleLog.Log(ex);
    }
}

public static List<ContextMenuItem> GetItems(this ContextMenuOpenedArgs args) {
    try {
        var itemList = (List<ContextMenuItem>)args.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(args);
        return itemList ?? new List<ContextMenuItem>();
    } catch (Exception ex) {
        SimpleLog.Log(ex);
        return new List<ContextMenuItem>();
    }
}
    
    
    
}