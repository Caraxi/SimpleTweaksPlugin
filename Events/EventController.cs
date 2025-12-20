using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JetBrains.Annotations;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Events;

public static unsafe class EventController {
    private static bool IsPointer<T>(this ParameterInfo p, int levels = 1) {
        var ptrType = p.ParameterType;
        for (var i = 0; i < levels; i++) {
            if (ptrType == null) return false;
            if (!ptrType.IsPointer) return false;
            if (!ptrType.HasElementType) return false;
            ptrType = ptrType.GetElementType();
        }

        return ptrType == typeof(T);
    }

    public class EventSubscriber {
        public BaseTweak Tweak { get; init; }
        public MethodInfo Method { get; init; }
        public SubscriberKind Kind { get; private set; } = SubscriberKind.Unknown;
        private Type addonPointerType;
        public uint NthTick { get; init; }
        private uint tick;

        public bool Enabled { get; set; } = true;
        
        public static EventSubscriber CreateFrameworkSubscriber(BaseTweak tweak, MethodInfo method, uint nthTick) {
            var s = new EventSubscriber {
                Tweak = tweak, Method = method, Kind = SubscriberKind.Framework, NthTick = nthTick,
            };
            return s;
        }

        public static EventSubscriber CreateTerritoryChangedSubscriber(BaseTweak tweak, MethodInfo method) {
            var s = new EventSubscriber { Tweak = tweak, Method = method, Kind = SubscriberKind.TerritoryChanged, };
            return s;
        }

        public enum SubscriberKind {
            Unknown,
            Invalid,
            Error,
            Framework,
            NoParameter,
            AtkUnitBase, // (AtkUnitBase*)
            AtkUnitBaseWithArrays, // (AtkUnitBase*, NumberArrayData**, StringArrayData**)
            AddonPointer, // (AddonX*)
            AddonPointerWithArrays, // (AddonX*, NumberArrayData**. StringArrayData**) 
            AddonArgs,
            AddonSetupArgs,
            AddonRequestedUpdateArgs,
            AddonRefreshArgs,
            AddonReceiveEventArgs,
            TerritoryChanged,
        }

        private bool IsAddonPointer(ParameterInfo p) {
            if (!p.ParameterType.IsPointer) return false;
            var elementType = p.ParameterType.GetElementType();
            if (elementType == null || elementType.IsPointer) return false;
            var addonAttribute = elementType.GetCustomAttribute<AddonAttribute>();
            if (addonAttribute == null) return false;
            return true;
        }

        private bool DetermineInvokeKind() {
            using var perf = PerformanceMonitor.Run("EventController::DetermineInvokeKind");
            var p = Method.GetParameters();

            try {
                if (p.Length == 0) {
                    Kind = SubscriberKind.NoParameter;
                    return true;
                } else if (p.Length == 1) {
                    if (IsAddonPointer(p[0])) {
                        Kind = SubscriberKind.AddonPointer;
                        addonPointerType = p[0].ParameterType;
                        return true;
                    }

                    if (p[0].IsPointer<AtkUnitBase>()) {
                        Kind = SubscriberKind.AtkUnitBase;
                        return true;
                    }

                    if (p[0].ParameterType == typeof(AddonArgs)) {
                        Kind = SubscriberKind.AddonArgs;
                        return true;
                    }

                    if (p[0].ParameterType == typeof(AddonSetupArgs)) {
                        Kind = SubscriberKind.AddonSetupArgs;
                        return true;
                    }

                    if (p[0].ParameterType == typeof(AddonRequestedUpdateArgs)) {
                        Kind = SubscriberKind.AddonRequestedUpdateArgs;
                        return true;
                    }

                    if (p[0].ParameterType == typeof(AddonRefreshArgs)) {
                        Kind = SubscriberKind.AddonRefreshArgs;
                        return true;
                    }

                    if (p[0].ParameterType == typeof(AddonReceiveEventArgs)) {
                        Kind = SubscriberKind.AddonReceiveEventArgs;
                        return true;
                    }
                } else if (p.Length == 3) {
                    if (p[1].IsPointer<NumberArrayData>(2) && p[2].IsPointer<StringArrayData>(2)) {
                        if (IsAddonPointer(p[0])) {
                            Kind = SubscriberKind.AddonPointer;
                            addonPointerType = p[0].ParameterType;
                            return true;
                        }

                        if (p[0].IsPointer<AtkUnitBase>()) {
                            Kind = SubscriberKind.AtkUnitBase;
                            return true;
                        }
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }

            SimpleLog.Error($"[EventController] Failed to determine a valid delegate type for '{Tweak.GetType().Name}.{Method.Name}'");

            foreach (var param in p) {
                SimpleLog.Error($"[EventController] \t - {param.ParameterType} {param.Name}");
            }

            Kind = SubscriberKind.Invalid;
            return false;
        }

        public void Invoke(object? args) {
            if (!Enabled) return;
            if (NthTick > 1) {
                if (++tick < NthTick) return;
                tick = 0;
            }

            if (Kind is SubscriberKind.Invalid or SubscriberKind.Error) return;
            if (Tweak.IsDisposed) return;
            if (!Tweak.Enabled) return;

            if (Kind == SubscriberKind.Unknown) {
                if (!DetermineInvokeKind()) return;
            }

            using var perfMon = PerformanceMonitor.Run($"[Event] {Tweak.Key} :: {Method.Name}");

            try {
                var _ = Kind switch {
                    SubscriberKind.Invalid => null,
                    SubscriberKind.Unknown => null,
                    SubscriberKind.Framework => Method.Invoke(Tweak, []),
                    SubscriberKind.NoParameter => Method.Invoke(Tweak, []),
                    SubscriberKind.AtkUnitBase => Method.Invoke(Tweak, [Pointer.Box((void*)((AddonArgs)args!).Addon.Address, typeof(AtkUnitBase*))]),
                    SubscriberKind.AtkUnitBaseWithArrays => Method.Invoke(Tweak, [Pointer.Box((void*)((AddonArgs)args!).Addon.Address, typeof(AtkUnitBase*)), Pointer.Box(AtkStage.Instance()->GetNumberArrayData(), typeof(NumberArrayData**)), Pointer.Box(AtkStage.Instance()->GetStringArrayData(), typeof(StringArrayData**))]),
                    SubscriberKind.AddonPointer => Method.Invoke(Tweak, [Pointer.Box((void*)((AddonArgs)args!).Addon.Address, addonPointerType)]),
                    SubscriberKind.AddonPointerWithArrays => Method.Invoke(Tweak, [Pointer.Box((void*)((AddonArgs)args!).Addon.Address, addonPointerType), Pointer.Box(AtkStage.Instance()->GetNumberArrayData(), typeof(NumberArrayData**)), Pointer.Box(AtkStage.Instance()->GetStringArrayData(), typeof(StringArrayData**))]),
                    SubscriberKind.AddonArgs => Method.Invoke(Tweak, [args]),
                    SubscriberKind.AddonSetupArgs when args is AddonSetupArgs addonSetupArgs => Method.Invoke(Tweak, [addonSetupArgs]),
                    SubscriberKind.AddonRequestedUpdateArgs when args is AddonRequestedUpdateArgs addonRequestedUpdateArgs => Method.Invoke(Tweak, [addonRequestedUpdateArgs]),
                    SubscriberKind.AddonRefreshArgs when args is AddonRefreshArgs addonRefreshArgs => Method.Invoke(Tweak, [addonRefreshArgs]),
                    SubscriberKind.AddonReceiveEventArgs when args is AddonReceiveEventArgs addonReceiveEventArgs => Method.Invoke(Tweak, [addonReceiveEventArgs]),
                    SubscriberKind.TerritoryChanged => Method.Invoke(Tweak, [args]),
                    _ => null,
                };
            } catch (Exception ex) {
                SimpleLog.Error($"Error invoking {Tweak.Key} :: {Method.Name}. Event has been disabled.");
                Kind = SubscriberKind.Error;
                SimpleTweaksPlugin.Plugin.Error(Tweak, ex, true);
            }
        }
    }

    private static Dictionary<AddonEvent, Dictionary<string, List<EventSubscriber>>> AddonEventSubscribers { get; } = new();
    private static List<EventSubscriber> FrameworkUpdateSubscribers { get; } = [];
    private static List<EventSubscriber> TerritoryChangedSubscribers { get; } = [];

    private static bool TryGetCustomAttribute<T>(this MemberInfo element, [NotNullWhen(true)] out T? attribute) where T : Attribute {
        attribute = element.GetCustomAttribute<T>();
        return attribute != null;
    }

    public static void RegisterEvents(BaseTweak? tweak) {
        if (tweak == null) return;
        if (tweak.IsDisposed) return;

        var methods = tweak.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var method in methods) {
            if (method.TryGetCustomAttribute<FrameworkUpdateAttribute>(out var fwUpdateAttribute)) {
                var subscriber = EventSubscriber.CreateFrameworkSubscriber(tweak, method, fwUpdateAttribute.NthTick);

                if (FrameworkUpdateSubscribers.Count == 0) {
                    Service.Framework.Update -= HandleFrameworkUpdate;
                    Service.Framework.Update += HandleFrameworkUpdate;
                }

                FrameworkUpdateSubscribers.Add(subscriber);
            }

            if (method.TryGetCustomAttribute<TerritoryChangedAttribute>(out _)) {
                var subscriber = EventSubscriber.CreateTerritoryChangedSubscriber(tweak, method);

                if (TerritoryChangedSubscribers.Count == 0) {
                    Service.ClientState.TerritoryChanged -= HandleTerritoryChanged;
                    Service.ClientState.TerritoryChanged += HandleTerritoryChanged;
                }

                TerritoryChangedSubscribers.Add(subscriber);
            }

            foreach (var attr in method.GetCustomAttributes<AddonEventAttribute>()) {
                foreach (var addon in attr.AddonNames) {
                    var subscriber = new EventSubscriber { Tweak = tweak, Method = method, Enabled = attr.AutoEnable };

                    foreach (var e in attr.Event) {
                        if (e is AddonEvent.PreReceiveEvent or AddonEvent.PostReceiveEvent && addon == "ALL_ADDONS") {
                            SimpleLog.Error($"[EventController] {tweak.Name} requesting event '{attr.Event}' on method '{method.Name}' for addon '{addon}' - NOT SUPPORTED");
                            continue;
                        }

                        SimpleLog.Verbose($"[EventController] {tweak.Name} requesting event '{attr.Event}' on method '{method.Name}' for addon '{addon}'");
                        if (!AddonEventSubscribers.TryGetValue(e, out var addonSubscriberDict)) {
                            addonSubscriberDict = new Dictionary<string, List<EventSubscriber>>();
                            AddonEventSubscribers.Add(e, addonSubscriberDict);
                            if (e is not (AddonEvent.PreReceiveEvent or AddonEvent.PostReceiveEvent)) {
                                Service.AddonLifecycle.RegisterListener(e, HandleEvent);
                            }
                        }

                        if (!addonSubscriberDict.TryGetValue(addon, out var addonSubscriberList)) {
                            addonSubscriberList = [];
                            addonSubscriberDict.Add(addon, addonSubscriberList);
                            if (e is AddonEvent.PreReceiveEvent or AddonEvent.PostReceiveEvent) {
                                Service.AddonLifecycle.RegisterListener(e, addon, HandleEvent);
                            }
                        }

                        addonSubscriberList.Add(subscriber);
                    }
                }
            }
        }
    }

    public static void DisableEvent(BaseTweak tweak, AddonEvent eventType, string addon, string methodName) {
        if (tweak == null) return;
        if (!AddonEventSubscribers.TryGetValue(eventType, out var addonSubscriberDict)) return;
        if (!addonSubscriberDict.TryGetValue(addon, out var addonSubscriberList)) return;
        foreach (var s in addonSubscriberList.Where(s => s.Tweak == tweak && s.Method.Name == methodName && s.Enabled)) {
            SimpleLog.Verbose($"[EventController] {tweak.Name} disabled event '{eventType}' on method '{methodName}' for addon '{addon}'");
            s.Enabled = false;
        }
    }
    
    public static void EnableEvent(BaseTweak tweak, AddonEvent eventType, string addon, string methodName) {
        if (tweak == null) return;
        if (!AddonEventSubscribers.TryGetValue(eventType, out var addonSubscriberDict)) return;
        if (!addonSubscriberDict.TryGetValue(addon, out var addonSubscriberList)) return;
        foreach (var s in addonSubscriberList.Where(s => s.Tweak == tweak && s.Method.Name == methodName && !s.Enabled)) {
            SimpleLog.Verbose($"[EventController] {tweak.Name} enabled event '{eventType}' on method '{methodName}' for addon '{addon}'");
            s.Enabled = true;
        }
    }
    
    private static void HandleFrameworkUpdate(IFramework framework) {
        foreach (var fwSubscriber in FrameworkUpdateSubscribers) {
            fwSubscriber.Invoke(null);
        }
    }

    private static void HandleTerritoryChanged(ushort newTerritory) {
        foreach (var tcSubscriber in TerritoryChangedSubscribers) {
            tcSubscriber.Invoke(newTerritory);
        }
    }

    private static void HandleEvent(AddonEvent type, AddonArgs args) {
        if (!AddonEventSubscribers.TryGetValue(type, out var addonSubscriberDict)) return;
        if (addonSubscriberDict.TryGetValue(args.AddonName, out var addonSubscriberList)) {
            foreach (var subscriber in addonSubscriberList) {
                if (subscriber.Tweak.IsDisposed) continue;
                if (!subscriber.Tweak.Enabled) continue;
                subscriber.Invoke(args);
            }
        }

        if (addonSubscriberDict.TryGetValue("ALL_ADDONS", out var allAddonSubscriberList)) {
            foreach (var subscriber in allAddonSubscriberList) {
                if (subscriber.Tweak.IsDisposed) continue;
                if (!subscriber.Tweak.Enabled) continue;
                subscriber.Invoke(args);
            }
        }
    }

    public static void UnregisterEvents(BaseTweak tweak) {
        FrameworkUpdateSubscribers.RemoveAll(f => f.Tweak == tweak);
        if (FrameworkUpdateSubscribers.Count == 0) {
            Service.Framework.Update -= HandleFrameworkUpdate;
        }

        TerritoryChangedSubscribers.RemoveAll(f => f.Tweak == tweak);
        if (TerritoryChangedSubscribers.Count == 0) {
            Service.ClientState.TerritoryChanged -= HandleTerritoryChanged;
        }

        foreach (var (_, addonSubscribers) in AddonEventSubscribers) {
            foreach (var (_, subscribers) in addonSubscribers) {
                subscribers.RemoveAll(subscriber => subscriber.Tweak == tweak);
            }
        }
    }
}
