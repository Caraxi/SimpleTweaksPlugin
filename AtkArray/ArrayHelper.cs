using System;
using System.Collections.Generic;
using SimpleTweaksPlugin.AtkArray.NumberArrays;
using SimpleTweaksPlugin.AtkArray.StringArrays;

namespace SimpleTweaksPlugin.AtkArray;

public static class ArrayHelper {
    public static readonly Dictionary<int, NumberArray> NumberArrays = new();
    public static readonly Dictionary<int, StringArray> StringArrays = new();
    
    public static readonly Dictionary<Type, int> NumberArrayTypes = new();
    public static readonly Dictionary<Type, int> StringArrayTypes = new();
    
    
    static ArrayHelper() {
        foreach (var t in typeof(NumberArray).Assembly.GetTypes()) {
            if (t.IsAbstract) continue;
            if (t.IsSubclassOf(typeof(NumberArray))) {
                var i = (NumberArray)Activator.CreateInstance(t);
                if (i != null) {
                    if (NumberArrays.TryAdd(i.NumberArrayIndex, i)) {
                        NumberArrayTypes[t] = i.NumberArrayIndex;
                    }
                }
            } else if (t.IsSubclassOf(typeof(StringArray))) {
                var i = (StringArray)Activator.CreateInstance(t);
                if (i != null) {
                    if (StringArrays.TryAdd(i.StringArrayIndex, i)) {
                        StringArrayTypes[t] = i.StringArrayIndex;
                    }
                }
            }
        }
    }

    public static T? GetNumberArray<T>() where T : NumberArray {
        return GetNumberArray(typeof(T)) as T;
    }
    
    public static NumberArray GetNumberArray(Type type) {
        return !NumberArrayTypes.TryGetValue(type, out var index) ? null : NumberArrays.GetValueOrDefault(index);
    }
    
    public static T? GetStringArray<T>() where T : StringArray {
        return GetStringArray(typeof(T)) as T;
    }
    
    public static StringArray GetStringArray(Type type) {
        return !StringArrayTypes.TryGetValue(type, out var index) ? null : StringArrays.GetValueOrDefault(index);
    }
}
