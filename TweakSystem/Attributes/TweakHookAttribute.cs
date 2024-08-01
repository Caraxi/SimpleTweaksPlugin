using System;
using System.Collections.Generic;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using InteropGenerator.Runtime;
using JetBrains.Annotations;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.TweakSystem;

[AttributeUsage(AttributeTargets.Field)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakHookAttribute : Attribute {
    public bool AutoEnable { get; init; } = true;
    public Type AddressType { get; init; } = null;
    public string AddressName { get; init; } = string.Empty;
    public string DetourName { get; init; } = string.Empty;

    public TweakHookAttribute() { }

    public TweakHookAttribute(Type type, string function, string detourName) {
        AddressType = type;
        AddressName = function;
        DetourName = detourName;
    }
}

public abstract class TweakHookMethodAttribute : Attribute {
    public abstract void Setup(BaseTweak tweak, MethodInfo methodInfo);
    public abstract IHookWrapper GetWrappedHook(BaseTweak tweak, MethodInfo methodInfo);
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakHookAttribute<T> : TweakHookMethodAttribute where T : Delegate {
    public static HookWrapper<T> GetHook(BaseTweak tweak, MethodInfo methodInfo) {
        return Hooks.GetValueOrDefault((tweak, methodInfo));
    }

    public override IHookWrapper GetWrappedHook(BaseTweak tweak, MethodInfo methodInfo) {
        return Hooks.GetValueOrDefault((tweak, methodInfo));
    }

    private static readonly Dictionary<(BaseTweak, MethodInfo), HookWrapper<T>> Hooks = new();

    public string Signature { get; }
    public int SignatureOffset { get; }
    public Type HookType { get; }
    public string HookFunction { get; }
    public bool AutoEnable { get; init; } = true;

    public override void Setup(BaseTweak tweak, MethodInfo methodInfo) {
        if (!Hooks.TryGetValue((tweak, methodInfo), out var hook) || hook == null) {
            try {
                if (HookType == null) {
                    SimpleLog.Debug($"{methodInfo}");
                    SimpleLog.Debug($"{typeof(T).GetMethod("Invoke")}");

                    var d = methodInfo.CreateDelegate(typeof(T), tweak) as T;
                    if (d == null) {
                        SimpleLog.Error("Failed to create delegate.");
                        return;
                    }

                    hook = Common.Hook(Signature, d, SignatureOffset);
                } else {
                    var addressesType = HookType.GetNestedType("Addresses");
                    if (addressesType == null) {
                        SimpleLog.Error($"Failed to find {HookType.Name}.Addresses");
                        return;
                    }

                    var addressField = addressesType.GetField(HookFunction);

                    if (addressField == null) {
                        SimpleLog.Error($"Failed to find {HookType.Name}.Addresses.{HookFunction}");
                        return;
                    }

                    var addressObj = addressField.GetValue(null);

                    if (addressObj is not Address address) {
                        SimpleLog.Error($"{HookType.Name}.Addresses.{HookFunction} is not an Address?");
                        return;
                    }

                    var d = methodInfo.CreateDelegate(typeof(T), tweak) as T;
                    if (d == null) {
                        SimpleLog.Error("Failed to create delegate.");
                        return;
                    }

                    hook = Common.Hook(address.Value, d);
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex, $"Error setting up TweakHook on '{tweak.Name}' {methodInfo.Name}");
            }
        }

        if (hook == null) {
            SimpleLog.Error($"TweakHook failed to setup for {tweak.Name} on Method {methodInfo.Name}");
            return;
        }

        if (hook.IsDisposed) {
            SimpleLog.Error($"TweakHook is Disposed for {tweak.Name} on Method {methodInfo.Name}");
            return;
        }

        if (AutoEnable) hook?.Enable();
        Hooks[(tweak, methodInfo)] = hook;
    }

    public TweakHookAttribute(string signature, int signatureOffset = 0) {
        Signature = signature;
        SignatureOffset = 0;
    }

    public TweakHookAttribute(Type type, string function) {
        HookType = type;
        HookFunction = function;
    }

    public TweakHookAttribute() {
        var t = typeof(T);
        var name = typeof(T).FullName;
        if (name == null) throw new Exception("Unsupported Type");
        if (!(name.StartsWith("FFXIVClientStructs.") && name.EndsWith($"+Delegates+{t.Name}"))) {
            throw new Exception("Unsupported Type");
        }

        var delegatesType = t.DeclaringType;
        if (delegatesType == null) throw new Exception("Unsupported Type");
        SimpleLog.Debug($"{delegatesType}");

        var owningType = delegatesType.DeclaringType;

        HookType = owningType ?? throw new Exception("Unsupported Type");
        HookFunction = t.Name;
    }
}
