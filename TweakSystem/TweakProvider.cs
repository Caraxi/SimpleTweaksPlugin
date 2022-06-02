using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Logging;

namespace SimpleTweaksPlugin.TweakSystem; 

public class TweakProvider : IDisposable {

    public bool IsDisposed { get; protected set; } = false;
    public List<BaseTweak> Tweaks { get; } = new();

    public Assembly Assembly { get; init; } = null!;

    public TweakProvider(Assembly assembly) {
        Assembly = assembly;
    }

    protected TweakProvider() { }

    public virtual void LoadTweaks() {
        foreach (var t in Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsAbstract)) {
            SimpleLog.Debug($"Initalizing Tweak: {t.Name}");
            try {
                var tweak = (Tweak) Activator.CreateInstance(t)!;
                tweak.InterfaceSetup(SimpleTweaksPlugin.Plugin, Service.PluginInterface, SimpleTweaksPlugin.Plugin.PluginConfig, this);
                if (tweak.CanLoad) {
                    var blacklistKey = tweak.Key;
                    if (tweak.Version > 1) blacklistKey += $"::{tweak.Version}";
                    if (SimpleTweaksPlugin.Plugin.PluginConfig.BlacklistedTweaks.Contains(blacklistKey)) {
                        SimpleLog.Log("Skipping blacklisted tweak: " + tweak.Key);
                        continue;
                    }
                    tweak.Setup();
                    if (tweak.Ready && (SimpleTweaksPlugin.Plugin.PluginConfig.EnabledTweaks.Contains(t.Name) || tweak is SubTweakManager {AlwaysEnabled: true})) {
                        SimpleLog.Debug($"Enable: {t.Name}");
                        try {
                            tweak.Enable();
                        } catch (Exception ex) {
                            SimpleTweaksPlugin.Plugin.Error(tweak, ex, true, $"Error in Enable for '{tweak.Name}");
                        }
                    }

                    Tweaks.Add(tweak);
                }
            } catch (Exception ex) {
                PluginLog.Error(ex, $"Failed loading tweak '{t.Name}'.");
            }
        }
        SimpleTweaksPlugin.Plugin.PluginConfig.RefreshSearch();
    }

    public void UnloadTweaks() {
        foreach (var t in Tweaks) {
            if (t.Enabled || t is SubTweakManager { AlwaysEnabled : true}) {
                SimpleLog.Log($"Disable: {t.Name}");
                try {
                    t.Disable();
                } catch (Exception ex) {
                    SimpleLog.Error($"Error in Disable for '{t.Name}'");
                    SimpleLog.Error(ex);
                }
            }
            SimpleLog.Log($"Dispose: {t.Name}");
            try {
                t.Dispose();
            } catch (Exception ex) {
                SimpleLog.Error($"Error in Dispose for '{t.Name}'");
                SimpleLog.Error(ex);
            }
        }
        Tweaks.Clear();
        SimpleTweaksPlugin.Plugin.PluginConfig.RefreshSearch();
    }


    public virtual void Dispose() {
        UnloadTweaks();
        IsDisposed = true;
    }
}