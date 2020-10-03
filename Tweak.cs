using System;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin {
    public abstract class Tweak : IDisposable {

        protected DalamudPluginInterface PluginInterface;

        public virtual bool Ready { get; protected set; }
        public virtual bool Enabled { get; protected set; }

        public abstract string Name { get; }

        public void InterfaceSetup(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
        }

        public virtual void Setup() {
            Ready = true;
        }

        public virtual void Enable() {
            Enabled = true;
        }

        public virtual void Disable() {
            Enabled = false;
        }

        public virtual void Dispose() {
            Enabled = false;
            Ready = false;
        }
    }
}
