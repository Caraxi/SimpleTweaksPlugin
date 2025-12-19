using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Lumina.Excel;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Utility; 

public static class Extensions {
    public static string GetDescription(this Enum @enum) {
        var eType = @enum.GetType();
        var member = eType.GetMember(@enum.ToString());
        if ((member.Length <= 0)) return @enum.ToString();
        var attribs = member[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
        return attribs.Length > 0 ? ((System.ComponentModel.DescriptionAttribute)attribs[0]).Description : @enum.ToString();
    }

    public static bool TryGetTooltip(this Enum @enum, [NotNullWhen(true)] out string? tooltip) {
        tooltip = string.Empty;
        var eType = @enum.GetType();
        var member = eType.GetMember(@enum.ToString());
        if (member.Length <= 0) return false;
        var attribs = member[0].GetCustomAttributes(typeof(EnumTooltipAttribute), false);
        tooltip =  attribs.Length > 0 ? ((EnumTooltipAttribute)attribs[0]).Text : null;
        return tooltip != null;
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
    public static bool Cutscene(this ICondition condition) => condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.OccupiedInCutSceneEvent];
    public static bool Duty(this ICondition condition) => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56];
    public static string GetKeyName(this VirtualKey k) => NamedKeys.ContainsKey(k) ? NamedKeys[k] : k.ToString();
    
    public static void Replace(this List<byte> byteList, IEnumerable<byte> search, IEnumerable<byte> replace) {
        for (var i = 0; i < byteList.Count; i++) {
            if (Equals(byteList.Skip(i).Take(search.Count()), search)) {
                
            }
        }
    }
        
    public static void AppendLine(this SeString str, List<Payload> payloads) {
        str.Append(payloads);
        str.Append(NewLinePayload.Payload);
    }
    public static void AppendLine(this SeString str, Payload payload) {
        str.Append(payload);
        str.Append(NewLinePayload.Payload);
    }
    public static void AppendLine(this SeString str, SeString other) {
        str.Append(other);
        str.Append(NewLinePayload.Payload);
    }
    public static void AppendLine(this SeString str, params Payload[] payloads) {
        str.Append(new List<Payload>(payloads));
        str.Append(NewLinePayload.Payload);
    }
    
    public static bool AnyExcept(this ICondition condition, params ConditionFlag[] exceptFlags) {
        for (int flag = 0; flag < 100; ++flag) {
            if (exceptFlags.Contains((ConditionFlag)flag)) continue;
            if (condition[flag]) return true;
        }
        return false;
    }
    
    public unsafe ref struct PointerSpanUnboxer<T> where T : unmanaged {
        private int currentIndex;
        private readonly Span<Pointer<T>> items;
        public PointerSpanUnboxer(Span<Pointer<T>> span) {
            items = span;
            currentIndex = -1;
        }

        public bool MoveNext() {
            currentIndex++;
            if (currentIndex >= items.Length) return false;
            if (items[currentIndex].Value == null) return MoveNext();
            return true;
        }

        public readonly T* Current => items[currentIndex].Value;
        public PointerSpanUnboxer<T> GetEnumerator() => new(items);
    }
    
    public static PointerReadOnlySpanUnboxer<T> Unbox<T>(this ReadOnlySpan<Pointer<T>> span) where T : unmanaged {
        return new PointerReadOnlySpanUnboxer<T>(span);
    }
    
    public unsafe ref struct PointerReadOnlySpanUnboxer<T> where T : unmanaged {
        private int currentIndex;
        private readonly ReadOnlySpan<Pointer<T>> items;
        public PointerReadOnlySpanUnboxer(ReadOnlySpan<Pointer<T>> span) {
            items = span;
            currentIndex = -1;
        }

        public bool MoveNext() {
            currentIndex++;
            if (currentIndex >= items.Length) return false;
            if (items[currentIndex].Value == null) return MoveNext();
            return true;
        }

        public readonly T* Current => items[currentIndex].Value;
        public PointerReadOnlySpanUnboxer<T> GetEnumerator() => new(items);
    }
    
    public static PointerSpanUnboxer<T> Unbox<T>(this Span<Pointer<T>> span) where T : unmanaged {
        return new PointerSpanUnboxer<T>(span);
    }

    internal static IEnumerable<(FieldInfo Field, TAttribute? Attribute)> GetFieldsWithAttribute<TAttribute>(this object obj, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) where TAttribute : Attribute {
        return obj.GetType().GetFields(flags).Select(f => (f, f.GetCustomAttribute<TAttribute>())).Where(f => f.Item2 != null);
    }
    
    public static unsafe string ValueString(this AtkValue v) {
        return v.Type switch {
            ValueType.Int => $"{v.Int}",
            ValueType.String => Marshal.PtrToStringUTF8(new IntPtr(v.String)),
            ValueType.UInt => $"{v.UInt}",
            ValueType.Bool => $"{v.Byte != 0}",
            ValueType.Float => $"{v.Float}",
            ValueType.Vector => "[Vector]",
            ValueType.ManagedString => Marshal.PtrToStringUTF8(new IntPtr(v.String))?.TrimEnd('\0') ?? string.Empty,
            ValueType.ManagedVector => "[Managed Vector]",
            _ => $"Unknown Type: {v.Type}"
        };
    }
    
    /// <summary> Return the first object fulfilling the predicate or null for structs. </summary>
    /// <param name="values"> The enumerable. </param>
    /// <param name="predicate"> The predicate. </param>
    /// <returns> The first object fulfilling the predicate, or a null-optional. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? FirstOrNull<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : struct
    {
        foreach(var val in values)
            if (predicate(val))
                return val;

        return null;
    }

    public static TExtension GetExtension<TExtension, TBase>(this TBase row) where TExtension : struct, IExcelRow<TExtension>, IRowExtension<TExtension, TBase> where TBase : struct, IExcelRow<TBase> => TExtension.GetExtended(row);

    public static bool TryGetAttribute<TAttribute>(this Type type, [NotNullWhen(true)] out TAttribute attribute) where TAttribute : Attribute {
        attribute = type.GetCustomAttribute<TAttribute>();
        return attribute != null;
    }

    public static bool IsPressed(this AtkEventData.AtkMouseData.ModifierFlag modifierFlag) {
        return 
            Service.KeyState[VirtualKey.SHIFT] == modifierFlag.HasFlag(AtkEventData.AtkMouseData.ModifierFlag.Shift) &&
            Service.KeyState[VirtualKey.MENU] == modifierFlag.HasFlag(AtkEventData.AtkMouseData.ModifierFlag.Alt) &&
            Service.KeyState[VirtualKey.CONTROL] == modifierFlag.HasFlag(AtkEventData.AtkMouseData.ModifierFlag.Ctrl);
    }
}