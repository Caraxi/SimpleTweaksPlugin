using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleTweaksPlugin.TweakSystem; 

public abstract class SubTweakManager : Tweak {
    public abstract List<BaseTweak> GetTweakList();
    public virtual bool AlwaysEnabled => false;

    public override void LanguageChanged() {
        foreach (var t in GetTweakList()) t.LanguageChanged();
    }
} 

public abstract class SubTweakManager<T> : SubTweakManager where T : BaseTweak {

    public List<BaseTweak> SubTweaks = new List<BaseTweak>();

    public override List<BaseTweak> GetTweakList() {
        return SubTweaks.Cast<BaseTweak>().ToList();
    }

    public string GetTweakKey(T t) {
        return $"{GetType().Name}@{t.GetType().Name}";
    }

    public override void Setup() {

        var tweakList = new List<BaseTweak>();

        foreach (var t in GetType().Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(T)))) {
            try {
                var tweak = (T) Activator.CreateInstance(t);
                if (tweak == null) continue;
                var blacklistKey = tweak.Key;
                if (tweak.Version > 1) blacklistKey += $"::{tweak.Version}";
                if (PluginConfig.BlacklistedTweaks.Contains(blacklistKey)) {
                    SimpleLog.Log("Skipping blacklisted tweak: " + tweak.Key);
                    var blTweak = new BlacklistedTweak(tweak.Key, tweak.Name, "Disabled due to known issues.");
                    blTweak.InterfaceSetup(SimpleTweaksPlugin.Plugin, Service.PluginInterface, SimpleTweaksPlugin.Plugin.PluginConfig, this.TweakProvider, this);
                    tweakList.Add(blTweak);
                    continue;
                }
                tweak.InterfaceSetup(this.Plugin, this.PluginInterface, this.PluginConfig, this.TweakProvider, this);
                if (tweak is not IDisabledTweak) {
                    tweak.Setup();
                }
                tweakList.Add(tweak);
            } catch (Exception ex) {
                Plugin.Error(this, ex, true, $"Error in Setup of '{t.Name}' @ '{this.Name}'");
            }
        }

        SubTweaks = tweakList.OrderBy(t => t.Name).ToList();

        Ready = true;
    }

    public override void RequestSaveConfig() {
        base.RequestSaveConfig();
        foreach (var t in SubTweaks) {
            t.RequestSaveConfig();
        }
    }

    protected override void Enable() {
        if (!Ready) return;
        foreach (var t in SubTweaks) {
            if (t is IDisabledTweak) continue;
            if (PluginConfig.EnabledTweaks.Contains(GetTweakKey(t as T))) {
                try {
                    SimpleLog.Log($"Enable: {t.Name} @ {Name}");
                    t.InternalEnable();
                } catch (Exception ex) {
                    Plugin.Error(this, t, ex, true, $"Error in Enable for '{t.Name}' @ '{this.Name}'");
                }
            }
        }
        Enabled = true;
    }

    protected override void Disable() {
        foreach (var t in SubTweaks.Where(t => t.Enabled)) {
            try {
                SimpleLog.Log($"Disable: {t.Name} @ {Name}");
                t.InternalDisable();
            } catch (Exception ex) {
                Plugin.Error(this, t, ex, true, $"Error in Disable for '{t.Name}' @ '{this.Name}'");
            }
                
        }
        Enabled = false;
    }

    public override void Dispose() {
        foreach (var t in SubTweaks) {
            try {
                if (t.Enabled) {
                    SimpleLog.Log($"Disable: {t.Name}");
                    t.InternalDisable();
                }
                SimpleLog.Log($"Dispose: {t.Name}");
                t.Dispose();
            } catch (Exception ex) {
                Plugin.Error(this, t, ex, true, $"Error in Dispose for '{t.Name}' @ '{this.Name}'");
            }
        }

        Ready = false;
    }
}