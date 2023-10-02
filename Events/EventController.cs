using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Events; 

public static unsafe class EventController {

    private static bool TryGetCustomAttribute<T>(this MemberInfo element, out T attribute) where T : Attribute {
        attribute = element.GetCustomAttribute<T>();
        return attribute != null;
    }

    public delegate void AddonDelegate(AtkUnitBase* addon);
    public delegate void AddonUpdateDelegate(AtkUnitBase* addon, NumberArrayData** numberArrays, StringArrayData** stringArrays);

    private delegate void UpdateAddonByID(RaptureAtkUnitManager* atkUnitManager, ushort addonId, NumberArrayData** numberArrays, StringArrayData** stringArrays, byte force);

    private record Subscriber<T>(BaseTweak Tweak, T Handler) where T : Delegate {
        public uint NthTick { get; init; } = 0;
    };
    
    private static readonly Dictionary<string, List<Subscriber<AddonDelegate>>> AddonPreSetupSubscribers = new();
    private static readonly Dictionary<string, List<Subscriber<AddonDelegate>>> AddonSetupSubscribers = new();
    private static readonly Dictionary<string, List<Subscriber<AddonDelegate>>> AddonFinalizeSubscribers = new();
    private static readonly Dictionary<string, List<Subscriber<AddonUpdateDelegate>>> AddonPreUpdateSubscribers = new();
    private static readonly Dictionary<string, List<Subscriber<AddonUpdateDelegate>>> AddonPostUpdateSubscribers = new();
    private static readonly List<Subscriber<Action>> FrameworkUpdateSubscribers = new();
    
    private static HookWrapper<UpdateAddonByID> _updateAddonByIdHook;
    
    static EventController() {
        Common.AddonSetup += HandleAddonSetup;
        Common.AddonFinalize += HandleAddonFinalize;
        Common.FrameworkUpdate += HandleFrameworkUpdate;
        
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, HandlePreRequestedUpdate);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, HandlePostRequestedUpdate);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, HandlePreSetup);
    }

    internal static void Destroy() {
        Common.AddonSetup -= HandleAddonSetup;
        Common.AddonFinalize -= HandleAddonFinalize;
        Common.FrameworkUpdate -= HandleFrameworkUpdate;

        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, HandlePreRequestedUpdate);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, HandlePostRequestedUpdate);
    }

    private static void HandlePreRequestedUpdate(AddonEvent eventType, AddonArgs addonInfo) {
        var stringArrays = AtkStage.GetSingleton()->GetStringArrayData();
        var numberArrays = AtkStage.GetSingleton()->GetNumberArrayData();
        HandleAddonPreUpdate(addonInfo.AddonName, (AtkUnitBase*) addonInfo.Addon, numberArrays, stringArrays);
    }
    
    private static void HandlePostRequestedUpdate(AddonEvent eventType, AddonArgs addonInfo) {
        var stringArrays = AtkStage.GetSingleton()->GetStringArrayData();
        var numberArrays = AtkStage.GetSingleton()->GetNumberArrayData();
        HandleAddonPostUpdate(addonInfo.AddonName, (AtkUnitBase*) addonInfo.Addon, numberArrays, stringArrays);
    }

    private static void HandlePreSetup(AddonEvent eventType, AddonArgs addonInfo) => HandleAddonEvent(AddonPreSetupSubscribers, addonInfo.AddonName, (AtkUnitBase*) addonInfo.Addon);

    private static void UpdateAddonByIdDetour(RaptureAtkUnitManager* atkUnitManager, ushort addonId, NumberArrayData** numberArrays, StringArrayData** stringArrays, byte forceB) {
        // Fully replace the function
        var didForward = false;
        try {
            if (addonId == 0) return;
            var addon = atkUnitManager->GetAddonById(addonId);
            if (addon == null) return;
            if (addon->ID != addonId) {
                throw new Exception("Addon with the incorrect ID was received");
            }
            if (addon->Name == null) {
                throw new Exception("Addon with a null name was received");
            }
            var name = Common.ReadString(addon->Name, 0x20);

            if (AddonPostUpdateSubscribers.ContainsKey(name) || AddonPreUpdateSubscribers.ContainsKey(name)) {
                if (!(forceB != 0 ||
                      ((*(uint*)(addon + 0x180) >> 0x14) & 0xF) != 5 ||
                      (*(byte*)(addon + 0x18A) & 0x10) != 0
                    )) return;
                var updateFunction = (delegate* unmanaged[Stdcall] <AtkUnitBase*, NumberArrayData**, StringArrayData**, void>)((void**)addon->VTable)[50];
                HandleAddonPreUpdate(name, addon, numberArrays, stringArrays);
                updateFunction(addon, numberArrays, stringArrays);
                didForward = true;
                HandleAddonPostUpdate(name, addon, numberArrays, stringArrays);
            } else {
                _updateAddonByIdHook.Original(atkUnitManager, addonId, numberArrays, stringArrays, forceB);
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            if (!didForward) _updateAddonByIdHook.Original(atkUnitManager, addonId, numberArrays, stringArrays, forceB);
        }
    }

    private static void HandleAddonSetup(SetupAddonArgs obj) => HandleAddonEvent(AddonSetupSubscribers, obj.AddonName, obj.Addon);
    private static void HandleAddonFinalize(SetupAddonArgs obj) => HandleAddonEvent(AddonFinalizeSubscribers, obj.AddonName, obj.Addon);

    private static void HandleAddonEvent(Dictionary<string, List<Subscriber<AddonDelegate>>> addonSubscriberDictionary, string addonName, AtkUnitBase* addon) {
        if (!addonSubscriberDictionary.TryGetValue(addonName, out var subscribers)) return;
        foreach (var subscriber in subscribers) {
            if (subscriber.Tweak.IsDisposed) continue;
            if (!subscriber.Tweak.Enabled) continue;
            subscriber.Handler(addon);
        }
    }

    private static void HandleAddonPreUpdate(string addonName, AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) => HandleAddonUpdate(AddonPreUpdateSubscribers, addonName, addon, numberArrayData, stringArrayData);
    private static void HandleAddonPostUpdate(string addonName, AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) => HandleAddonUpdate(AddonPostUpdateSubscribers, addonName, addon, numberArrayData, stringArrayData);
    
    private static void HandleAddonUpdate(Dictionary<string, List<Subscriber<AddonUpdateDelegate>>> addonSubscriberDictionary, string addonName, AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        try {
            if (!addonSubscriberDictionary.TryGetValue(addonName, out var subscribers)) return;
            foreach (var subscriber in subscribers) {
                if (subscriber.Tweak.IsDisposed) continue;
                if (!subscriber.Tweak.Enabled) continue;
                subscriber.Handler(addon, numberArrayData, stringArrayData);
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }
    
    private static void HandleFrameworkUpdate() {
        foreach (var subscriber in FrameworkUpdateSubscribers) {
            if (subscriber.NthTick != 0 && Framework.Instance()->FrameCounter % subscriber.NthTick != 0) continue;
            if (subscriber.Tweak.IsDisposed) continue;
            if (!subscriber.Tweak.Enabled) continue;
            subscriber.Handler();
        }
    }
    
    private static AddonUpdateDelegate WrapAddonUpdate(AddonDelegate wrap) => (addon, _, _) => wrap(addon);

    public static void RegisterEvents(BaseTweak tweak) {
        if (tweak == null) return;
        if (tweak.IsDisposed) return;

        var methods = tweak.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods) {
            if (method.TryGetCustomAttribute<AddonSetupAttribute>(out var addonSetupAttribute)) {
                try {
                    var setupDelegate = method.CreateDelegate<AddonDelegate>(tweak);
                    RegisterAddonSetup(tweak, setupDelegate, addonSetupAttribute.AddonNames);
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind AddonSetup to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
            
            if (method.TryGetCustomAttribute<AddonPreSetupAttribute>(out var addonPreSetupAttribute)) {
                try {
                    var setupDelegate = method.CreateDelegate<AddonDelegate>(tweak);
                    RegisterAddonPreSetup(tweak, setupDelegate, addonPreSetupAttribute.AddonNames);
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind AddonPreSetup to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
            
            if (method.TryGetCustomAttribute<AddonFinalizeAttribute>(out var addonFinalizeAttribute)) {
                try {
                    var finalizeDelegate = method.CreateDelegate<AddonDelegate>(tweak);
                    RegisterAddonFinalize(tweak, finalizeDelegate, addonFinalizeAttribute.AddonNames);
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind AddonFinalize to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
            
            if (method.TryGetCustomAttribute<AddonPreUpdateAttribute>(out var addonPreUpdateAttribute)) {
                try {

                    try {
                        var updateDelegate = method.CreateDelegate<AddonUpdateDelegate>(tweak);
                        RegisterAddonPreUpdate(tweak, updateDelegate, addonPreUpdateAttribute.AddonNames);
                    } catch (ArgumentException) {
                        var updateDelegate = method.CreateDelegate<AddonDelegate>(tweak);
                        var wrapped = WrapAddonUpdate(updateDelegate);
                        RegisterAddonPreUpdate(tweak, wrapped, addonPreUpdateAttribute.AddonNames);
                    }
                    
                    _updateAddonByIdHook?.Enable();
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind AddonPreUpdate to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
            
            if (method.TryGetCustomAttribute<AddonPostUpdateAttribute>(out var addonPostUpdateAttribute)) {
                try {
                    try {
                        var updateDelegate = method.CreateDelegate<AddonUpdateDelegate>(tweak);
                        RegisterAddonPostUpdate(tweak, updateDelegate, addonPostUpdateAttribute.AddonNames);
                    } catch (ArgumentException) {
                        var updateDelegate = method.CreateDelegate<AddonDelegate>(tweak);
                        var wrapped = WrapAddonUpdate(updateDelegate);
                        RegisterAddonPostUpdate(tweak, wrapped, addonPostUpdateAttribute.AddonNames);
                    }
                    _updateAddonByIdHook?.Enable();
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind AddonPostUpdate to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
            
            if (method.TryGetCustomAttribute<FrameworkUpdateAttribute>(out var frameworkUpdateAttribute)) {
                try {
                    var finalizeDelegate = method.CreateDelegate<Action>(tweak);
                    RegisterFrameworkUpdate(tweak, finalizeDelegate, frameworkUpdateAttribute.NthTick);
                } catch (Exception ex) {
                    SimpleLog.Error($"Failed to bind FrameworkUpdate to {tweak.GetType().Name}.{method.Name}");
                    SimpleLog.Error(ex);
                }
            }
        }
    }

    public static void UnregisterEvents(BaseTweak tweak) {
        UnregisterAddonSetup(tweak);
        UnregisterAddonPreSetup(tweak);
        UnregisterAddonFinalize(tweak);
        UnregisterAddonPreUpdate(tweak);
        UnregisterAddonPostUpdate(tweak);
        UnregisterFrameworkUpdate(tweak);
    }
    
    private static void RegisterAddonSetup(BaseTweak tweak, AddonDelegate handler, params string[] addonNames) {
        foreach (var addonName in addonNames) {
            if (!AddonSetupSubscribers.ContainsKey(addonName)) AddonSetupSubscribers.Add(addonName, new List<Subscriber<AddonDelegate>>());
            if (!AddonSetupSubscribers.TryGetValue(addonName, out var subscribers)) continue;
            subscribers.Add(new Subscriber<AddonDelegate>(tweak, handler));
        }
    }
    
    private static void RegisterAddonPreSetup(BaseTweak tweak, AddonDelegate handler, params string[] addonNames) {
        foreach (var addonName in addonNames) {
            if (!AddonPreSetupSubscribers.ContainsKey(addonName)) AddonPreSetupSubscribers.Add(addonName, new List<Subscriber<AddonDelegate>>());
            if (!AddonPreSetupSubscribers.TryGetValue(addonName, out var subscribers)) continue;
            subscribers.Add(new Subscriber<AddonDelegate>(tweak, handler));
        }
    }
    
    private static void RegisterAddonFinalize(BaseTweak tweak, AddonDelegate handler, params string[] addonNames) {
        foreach (var addonName in addonNames) {
            if (!AddonFinalizeSubscribers.ContainsKey(addonName)) AddonFinalizeSubscribers.Add(addonName, new List<Subscriber<AddonDelegate>>());
            if (!AddonFinalizeSubscribers.TryGetValue(addonName, out var subscribers)) continue;
            subscribers.Add(new Subscriber<AddonDelegate>(tweak, handler));
        }
    }

    private static void RegisterAddonPreUpdate(BaseTweak tweak, AddonUpdateDelegate handler, params string[] addonNames) {
        foreach (var addonName in addonNames) {
            if (!AddonPreUpdateSubscribers.ContainsKey(addonName)) AddonPreUpdateSubscribers.Add(addonName, new List<Subscriber<AddonUpdateDelegate>>());
            if (!AddonPreUpdateSubscribers.TryGetValue(addonName, out var subscribers)) continue;
            subscribers.Add(new Subscriber<AddonUpdateDelegate>(tweak, handler));
        }
    }
    
    private static void RegisterAddonPostUpdate(BaseTweak tweak, AddonUpdateDelegate handler, params string[] addonNames) {
        foreach (var addonName in addonNames) {
            if (!AddonPostUpdateSubscribers.ContainsKey(addonName)) AddonPostUpdateSubscribers.Add(addonName, new List<Subscriber<AddonUpdateDelegate>>());
            if (!AddonPostUpdateSubscribers.TryGetValue(addonName, out var subscribers)) continue;
            subscribers.Add(new Subscriber<AddonUpdateDelegate>(tweak, handler));
        }
    }

    private static void RegisterFrameworkUpdate(BaseTweak tweak, Action handler, uint nthTick = 0) {
        FrameworkUpdateSubscribers.Add(new Subscriber<Action>(tweak, handler) {
            NthTick = nthTick
        });
    }

    private static void UnregisterAddonSetup(BaseTweak tweak) {
        foreach (var subscribers in AddonSetupSubscribers.Values) {
            subscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
        }
    }
    
    private static void UnregisterAddonPreSetup(BaseTweak tweak) {
        foreach (var subscribers in AddonPreSetupSubscribers.Values) {
            subscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
        }
    }
    
    private static void UnregisterAddonFinalize(BaseTweak tweak) {
        foreach (var subscribers in AddonFinalizeSubscribers.Values) {
            subscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
        }
    }

    private static void UnregisterAddonPreUpdate(BaseTweak tweak) {
        foreach (var (addon, subscribers) in AddonPreUpdateSubscribers.ToArray()) {
            subscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
            if (subscribers.Count == 0) {
                AddonPreUpdateSubscribers.Remove(addon);
            }
        }
        
        if (AddonPostUpdateSubscribers.Count == 0 && AddonPreUpdateSubscribers.Count == 0) 
            _updateAddonByIdHook?.Disable();
    }
    
    private static void UnregisterAddonPostUpdate(BaseTweak tweak) {
        foreach (var (addon, subscribers) in AddonPostUpdateSubscribers.ToArray()) {
            subscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
            if (subscribers.Count == 0) {
                AddonPostUpdateSubscribers.Remove(addon);
            }
        }

        if (AddonPostUpdateSubscribers.Count == 0 && AddonPreUpdateSubscribers.Count == 0) 
            _updateAddonByIdHook?.Disable();
    }

    private static void UnregisterFrameworkUpdate(BaseTweak tweak) {
        FrameworkUpdateSubscribers.RemoveAll(s => s.Tweak.Key == tweak.Key);
    }
}
