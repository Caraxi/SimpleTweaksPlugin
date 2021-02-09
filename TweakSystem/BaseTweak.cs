﻿using System.Numerics;
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
        protected virtual string Author => null;
        public virtual bool Experimental => false;

        public virtual bool CanLoad => true;

        public void InterfaceSetup(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface, SimpleTweaksPluginConfig config) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = config;
            this.Plugin = plugin;
        }

        private void DrawCommon() {
            if (this.Experimental) {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "  Experimental");
            }

            if (!string.IsNullOrEmpty(Author)) {
                ImGui.SameLine();
                ImGui.TextDisabled($"  by {Author}");
            }
        }
        
        public void DrawConfig(ref bool hasChanged) {
            if (DrawConfigTree != null && Enabled) {
                var x = ImGui.GetCursorPosX();
                if (ImGui.TreeNode($"{Name}##treeConfig_{GetType().Name}")) {
                    DrawCommon();
                    ImGui.SetCursorPosX(x);
                    ImGui.BeginGroup();
                    DrawConfigTree(ref hasChanged);
                    ImGui.EndGroup();
                    ImGui.TreePop();
                } else {
                    DrawCommon();
                }
            } else {
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                ImGui.TreeNodeEx(Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                DrawCommon();
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
