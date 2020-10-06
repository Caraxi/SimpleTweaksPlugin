using System;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin {
    public abstract class Tweak : IDisposable {
        protected DalamudPluginInterface PluginInterface;
        protected SimpleTweaksPluginConfig PluginConfig;

        public virtual bool Ready { get; protected set; }
        public virtual bool Enabled { get; protected set; }

        public abstract string Name { get; }

        public void InterfaceSetup(DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = config;
        }

        public virtual bool DrawConfig() {
            return false;
        }

        public abstract void Setup();

        public abstract void Enable();

        public abstract void Disable();

        public abstract void Dispose();
    }
}
