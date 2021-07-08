using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace SimpleTweaksPlugin.TweakSystem {

    public abstract class SubTweakManager : Tweak {
        public abstract List<BaseTweak> GetTweakList();
        public virtual bool AlwaysEnabled => false;
    } 

    public abstract class SubTweakManager<T> : SubTweakManager where T : BaseTweak {

        public List<T> SubTweaks = new List<T>();

        public override List<BaseTweak> GetTweakList() {
            return SubTweaks.Cast<BaseTweak>().ToList();
        }

        public string GetTweakKey(T t) {
            return $"{GetType().Name}@{t.GetType().Name}";
        }

        public override void Setup() {

            var tweakList = new List<T>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(T)))) {
                try {
                    var tweak = (T) Activator.CreateInstance(t);
                    tweak.InterfaceSetup(this.Plugin, this.PluginInterface, this.PluginConfig);
                    tweak.Setup();
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

        public override void Enable() {
            if (!Ready) return;
            foreach (var t in SubTweaks) {
                if (PluginConfig.EnabledTweaks.Contains(GetTweakKey(t))) {
                    try {
                        SimpleLog.Log($"Enable: {t.Name} @ {Name}");
                        t.Enable();
                    } catch (Exception ex) {
                        Plugin.Error(this, t, ex, true, $"Error in Enable for '{t.Name}' @ '{this.Name}'");
                    }
                }
            }
            Enabled = true;
        }

        public override void Disable() {
            foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                try {
                    SimpleLog.Log($"Disable: {t.Name} @ {Name}");
                    t.Disable();
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
                        t.Disable();
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
}
