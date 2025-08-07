using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace SimpleTweaksPlugin.Utility;

// https://github.com/goatcorp/Dalamud/tree/master/Dalamud/Utility/Signatures

public static class SignatureHelper {
    private static class NullabilityUtil {
        internal static bool IsNullable(PropertyInfo property) => IsNullableHelper(property.PropertyType, property.DeclaringType, property.CustomAttributes);

        internal static bool IsNullable(FieldInfo field) => IsNullableHelper(field.FieldType, field.DeclaringType, field.CustomAttributes);

        private static bool IsNullableHelper(Type memberType, MemberInfo? declaringType, IEnumerable<CustomAttributeData> customAttributes) {
            if (memberType.IsValueType) {
                return Nullable.GetUnderlyingType(memberType) != null;
            }

            var nullable = customAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
            if (nullable is { ConstructorArguments.Count: 1 }) {
                var attributeArgument = nullable.ConstructorArguments[0];
                if (attributeArgument.ArgumentType == typeof(byte[])) {
                    var args = (ReadOnlyCollection<CustomAttributeTypedArgument>)attributeArgument.Value!;
                    if (args.Count > 0 && args[0].ArgumentType == typeof(byte)) {
                        return (byte)args[0].Value! == 2;
                    }
                } else if (attributeArgument.ArgumentType == typeof(byte)) {
                    return (byte)attributeArgument.Value! == 2;
                }
            }

            for (var type = declaringType; type != null; type = type.DeclaringType) {
                var context = type.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
                if (context is { ConstructorArguments.Count: 1 } && context.ConstructorArguments[0].ArgumentType == typeof(byte)) {
                    return (byte)context.ConstructorArguments[0].Value! == 2;
                }
            }

            return false;
        }
    }

    private sealed class PropertyInfoWrapper : IFieldOrPropertyInfo {
        public PropertyInfoWrapper(PropertyInfo info) {
            Info = info;
        }

        public string Name => Info.Name;
        public Type ActualType => Info.PropertyType;
        public bool IsNullable => NullabilityUtil.IsNullable(Info);

        private PropertyInfo Info { get; }

        public void SetValue(object? self, object? value) {
            Info.SetValue(self, value);
        }

        public T? GetCustomAttribute<T>() where T : Attribute {
            return Info.GetCustomAttribute<T>();
        }
    }

    private sealed class FieldInfoWrapper : IFieldOrPropertyInfo {
        public FieldInfoWrapper(FieldInfo info) {
            Info = info;
        }

        public string Name => Info.Name;
        public Type ActualType => Info.FieldType;
        public bool IsNullable => NullabilityUtil.IsNullable(Info);
        private FieldInfo Info { get; }

        public void SetValue(object? self, object? value) {
            Info.SetValue(self, value);
        }

        public T? GetCustomAttribute<T>() where T : Attribute {
            return Info.GetCustomAttribute<T>();
        }
    }

    private interface IFieldOrPropertyInfo {
        string Name { get; }
        Type ActualType { get; }
        bool IsNullable { get; }
        void SetValue(object? self, object? value);
        T? GetCustomAttribute<T>() where T : Attribute;
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static PropertyInfo PIsFunctionPointer { get; } = typeof(Type).GetProperty("IsFunctionPointer", BindingFlags.Public | BindingFlags.Instance);
    private static PropertyInfo PIsUnmanagedFunctionPointer { get; } = typeof(Type).GetProperty("IsUnmanagedFunctionPointer", BindingFlags.Public | BindingFlags.Instance);

    public static void Initialise(object self, bool log = true) {
        var scanner = Service.SigScanner;
        var selfType = self.GetType();
        var fields = selfType.GetFields(Flags).Select(field => (IFieldOrPropertyInfo)new FieldInfoWrapper(field)).Concat(selfType.GetProperties(Flags).Select(prop => new PropertyInfoWrapper(prop))).Select(field => (field, field.GetCustomAttribute<SignatureAttribute>())).Where(field => field.Item2 != null);

        foreach (var (info, sig) in fields) {
            var wasWrapped = false;
            var actualType = info.ActualType;
            if (actualType.IsGenericType && actualType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                // unwrap the nullable
                actualType = actualType.GetGenericArguments()[0];
                wasWrapped = true;
            }

            var fallibility = sig!.Fallibility;
            if (fallibility == Fallibility.Auto) {
                fallibility = info.IsNullable || wasWrapped ? Fallibility.Fallible : Fallibility.Infallible;
            }

            #if TEST
            var fallible = false;
            #else
            var fallible = fallibility == Fallibility.Fallible;
            #endif
            

            void Invalid(string message, bool prepend = true) {
                var errorMsg = prepend ? $"Invalid Signature attribute for {selfType.FullName}.{info.Name}: {message}" : message;
                if (fallible) {
                    SimpleLog.Warning(errorMsg);
                } else {
                    throw new Exception(errorMsg);
                }
            }

            nint ptr;
            var success = sig.ScanType == ScanType.Text ? scanner.TryScanText(sig.Signature, out ptr) : scanner.TryGetStaticAddressFromSig(sig.Signature, out ptr);
            if (!success) {
                if (log) {
                    Invalid($"Failed to find {sig.ScanType} signature \"{info.Name}\" for {selfType.FullName} ({sig.Signature})", false);
                }

                continue;
            }

            
            // Hack to make it work for both NET7 and NET8
            bool isFunctionPointer;
            if (PIsFunctionPointer == null || PIsUnmanagedFunctionPointer == null) {
                // NET7 Compat
                isFunctionPointer = actualType.IsPointer;
            } else {
                // Net8 Compat
                isFunctionPointer = PIsFunctionPointer.GetValue(actualType) as bool? == true || PIsUnmanagedFunctionPointer.GetValue(actualType) as bool? == true;
            }
            
            // TODO: Don't do reflection
            
            switch (sig.UseFlags) {
                case SignatureUseFlags.Auto when actualType == typeof(IntPtr) || isFunctionPointer || actualType.IsAssignableTo(typeof(Delegate)):
                case SignatureUseFlags.Pointer: {
                    if (actualType.IsAssignableTo(typeof(Delegate))) {
                        info.SetValue(self, Marshal.GetDelegateForFunctionPointer(ptr, actualType));
                    } else {
                        info.SetValue(self, ptr);
                    }

                    break;
                }

                case SignatureUseFlags.Auto when actualType.IsGenericType && (actualType.GetGenericTypeDefinition() == typeof(Hook<>) || actualType.GetGenericTypeDefinition() == typeof(HookWrapper<>)):
                case SignatureUseFlags.Hook: {
                    var isHookWrapper = actualType.IsGenericType && actualType.GetGenericTypeDefinition() == typeof(HookWrapper<>);

                    if (!isHookWrapper && (!actualType.IsGenericType || actualType.GetGenericTypeDefinition() != typeof(Hook<>))) {
                        Invalid($"{actualType.Name} is not a Hook<T>");
                        continue;
                    }

                    var hookDelegateType = actualType.GenericTypeArguments[0];

                    Delegate? detour;
                    if (sig.DetourName == null) {
                        var matches = selfType.GetMethods(Flags).Select(method => method.IsStatic ? Delegate.CreateDelegate(hookDelegateType, method, false) : Delegate.CreateDelegate(hookDelegateType, self, method, false)).Where(del => del != null).ToArray();
                        if (matches.Length != 1) {
                            Invalid("Either found no matching detours or found more than one: specify a detour name");
                            continue;
                        }

                        detour = matches[0]!;
                    } else {
                        var method = selfType.GetMethod(sig.DetourName, Flags);
                        if (method == null) {
                            Invalid($"Could not find detour \"{sig.DetourName}\"");
                            continue;
                        }

                        var del = method.IsStatic ? Delegate.CreateDelegate(hookDelegateType, method, false) : Delegate.CreateDelegate(hookDelegateType, self, method, false);
                        if (del == null) {
                            Invalid($"Method {sig.DetourName} was not compatible with delegate {hookDelegateType.Name}");
                            continue;
                        }

                        detour = del;
                    }

                    var hookType = actualType;

                    if (isHookWrapper) {
                        hookType = actualType.GetField("wrappedHook", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
                    }

                    var createMethod = hookType.GetMethod("FromAddress", BindingFlags.Static | BindingFlags.NonPublic);
                    if (createMethod == null) {
                        SimpleTweaksPlugin.Plugin.Error(new Exception($"Error in SignatureHelper for {self.GetType().Name}: could not find Hook<{hookDelegateType.Name}>.FromAddress"));
                        continue;
                    }

                    var hook = createMethod.Invoke(null, new object[] { ptr, detour, false });

                    if (isHookWrapper) {
                        var wrapperCtor = actualType.GetConstructor(new[] { hookType });
                        if (wrapperCtor == null) {
                            SimpleTweaksPlugin.Plugin.Error(new Exception($"Error in SignatureHelper for {self.GetType().Name}: could not find could not find HookWrapper<{hookDelegateType.Name}> constructor"));
                            continue;
                        }

                        var wrapper = wrapperCtor.Invoke(new[] { hook });
                        SimpleLog.Verbose($"Created Hook Wrapper");
                        info.SetValue(self, wrapper);
                    } else {
                        info.SetValue(self, hook);
                    }

                    break;
                }

                case SignatureUseFlags.Auto when actualType.IsPrimitive:
                case SignatureUseFlags.Offset: {
                    var offset = Marshal.PtrToStructure(ptr + sig.Offset, actualType);
                    info.SetValue(self, offset);

                    break;
                }

                default: {
                    if (log) {
                        Invalid("could not detect desired signature use, set SignatureUseFlags manually");
                    }

                    break;
                }
            }
        }
    }
}
