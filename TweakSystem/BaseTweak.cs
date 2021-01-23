using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;

namespace SimpleTweaksPlugin.TweakSystem {
    public abstract class BaseTweak {
        protected SimpleTweaksPlugin Plugin;
        protected DalamudPluginInterface PluginInterface;
        protected SimpleTweaksPluginConfig PluginConfig;

        public virtual bool Ready { get; protected set; }
        public virtual bool Enabled { get; protected set; }

        public abstract string Name { get; }
        public virtual bool Experimental => false;

        public virtual bool CanLoad => true;

        public void InterfaceSetup(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = config;
            this.Plugin = plugin;
        }

        private void DrawExperimentalNotice() {
            if (this.Experimental) {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
            }
        }
        
        public virtual void DrawConfig(ref bool hasChanged) {
            if (DrawConfigTree != null && Enabled) {
                if (ImGui.TreeNode($"{Name}##treeConfig_{GetType().Name}")) {
                    DrawExperimentalNotice();
                    DrawConfigTree(ref hasChanged);
                    ImGui.TreePop();
                } else {
                    DrawExperimentalNotice();
                }
            } else {
                ImGui.Indent(56);
                ImGui.Text(Name);
                DrawExperimentalNotice();
                ImGui.Indent(-56);
            }
        }

        protected delegate void DrawConfigDelegate(ref bool hasChanged);
        protected virtual DrawConfigDelegate DrawConfigTree => null;
        
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
            Ready = false;
        }


    }
}
