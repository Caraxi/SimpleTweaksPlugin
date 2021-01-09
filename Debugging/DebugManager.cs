using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using SimpleTweaksPlugin.Debugging;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public DebugConfig Debugging = new DebugConfig();

        public bool ShouldSerializeDebugging() {
            return DebugManager.Enabled;
        }
        
    }
}

namespace SimpleTweaksPlugin.Debugging {

    public partial class DebugConfig {
        public string SelectedPage = String.Empty;
    }

    public abstract class DebugHelper : IDisposable {
        public SimpleTweaksPlugin Plugin;
        public abstract void Draw();
        public abstract string Name { get; }

        public virtual void Dispose() {
            
        }

    }
    
    public class DebugManager : IDisposable {

        private static Dictionary<string, Action> debugPages = new Dictionary<string, Action>();

        private static float sidebarSize = 0;
        
        public static bool Enabled = false;

        public static void RegisterDebugPage(string key, Action action) {
            if (debugPages.ContainsKey(key)) {
                debugPages[key] = action;
            } else {
                debugPages.Add(key, action);
            }

            sidebarSize = 0;
        }

        public static void RemoveDebugPage(string key) {
            if (debugPages.ContainsKey(key)) {
                debugPages.Remove(key);
            }

            sidebarSize = 0;
        }

        private static SimpleTweaksPlugin _plugin;

        private static bool _setupDebugHelpers = false;

        private static readonly List<DebugHelper> DebugHelpers = new List<DebugHelper>();

        public static void SetPlugin(SimpleTweaksPlugin plugin) {
            _plugin = plugin;
        }

        public static void DrawDebugWindow(ref bool open) {
            if (_plugin == null) return;

            if (!_setupDebugHelpers) {
                _setupDebugHelpers = true;
                foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(DebugHelper)) && !t.IsAbstract)) {
                    var debugger = (DebugHelper)Activator.CreateInstance(t);
                    debugger.Plugin = _plugin;
                    RegisterDebugPage(debugger.Name, debugger.Draw);
                    DebugHelpers.Add(debugger);
                }
            }

            if (sidebarSize < 150) {
                sidebarSize = 150;
                foreach (var k in debugPages.Keys) {
                    var s = ImGui.CalcTextSize(k).X + ImGui.GetStyle().FramePadding.X * 5 + ImGui.GetStyle().ScrollbarSize;
                    if (s > sidebarSize) {
                        sidebarSize = s;
                    }
                }
            }

            ImGui.SetNextWindowSize(new Vector2(500, 350) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350, 350) * ImGui.GetIO().FontGlobalScale, new Vector2(2000, 2000) * ImGui.GetIO().FontGlobalScale);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF000000);
            if (ImGui.Begin($"SimpleTweaksPlugin - Debug", ref open)) {
                
                if (ImGui.BeginChild("###debugPages", new Vector2(sidebarSize, -1) * ImGui.GetIO().FontGlobalScale, true)) {

                    foreach (var k in debugPages.Keys) {

                        if (ImGui.Selectable($"{k}##debugPageOption", _plugin.PluginConfig.Debugging.SelectedPage == k)) {
                            _plugin.PluginConfig.Debugging.SelectedPage = k;
                            _plugin.PluginConfig.Save();
                        }
                        
                    }

                    ImGui.EndChild();
                }

                ImGui.SameLine();

                if (ImGui.BeginChild("###debugView", new Vector2(-1, -1), true, ImGuiWindowFlags.HorizontalScrollbar)){
                    if (string.IsNullOrEmpty(_plugin.PluginConfig.Debugging.SelectedPage) || !debugPages.ContainsKey(_plugin.PluginConfig.Debugging.SelectedPage)) {
                        ImGui.Text("Select Debug Page");
                    } else {
                        debugPages[_plugin.PluginConfig.Debugging.SelectedPage]();
                    }
                    ImGui.EndChild();
                }


                ImGui.End();
            }
            ImGui.PopStyleColor();
        }

        public void Dispose() {
            foreach (var debugger in DebugHelpers) {
                RemoveDebugPage(debugger.Name);
                debugger.Dispose();
            }
            DebugHelpers.Clear();
        }


        private static unsafe Vector2 GetNodePosition(AtkResNode* node) {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null) {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }
            return pos;
        }

        private static unsafe Vector2 GetNodeScale(AtkResNode* node) {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null) {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }
            return scale;
        }

        private static unsafe bool GetNodeVisible(AtkResNode* node) {
            if (node == null) return false;
            while (node != null) {
                if ((node->Flags & (short)NodeFlags.Visible) != (short)NodeFlags.Visible) return false;
                node = node->ParentNode;
            }
            return true;
        }
        
        public static unsafe void HighlightResNode(AtkResNode* node) {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;

            var nodeVisible = GetNodeVisible(node);
            ImGui.GetForegroundDrawList().AddRectFilled(position, position+size, (uint) (nodeVisible ? 0x5500FF00 : 0x550000FF));
            ImGui.GetForegroundDrawList().AddRect(position, position + size, nodeVisible ? 0xFF00FF00 : 0xFF0000FF);
            
        }

        public static void ClickToCopyText(string text, string textCopy = null) {
            textCopy ??= text;
            ImGui.Text($"{text}");
            if (ImGui.IsItemHovered()) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (textCopy != text) ImGui.SetTooltip(textCopy);
            }
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{textCopy}");
        }

        public static unsafe void SeStringToText(SeString seStr) {
            var pushColorCount = 0;

            ImGui.BeginGroup();
            foreach (var p in seStr.Payloads) {
                switch (p) {
                    case UIForegroundPayload c:
                        if (c.ColorKey == 0) {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                            pushColorCount++;
                            break;
                        }
                        var r = (c.UIColor.UIForeground >> 0x18) & 0xFF;
                        var g = (c.UIColor.UIForeground >> 0x10) & 0xFF;
                        var b = (c.UIColor.UIForeground >> 0x08) & 0xFF;
                        var a = (c.UIColor.UIForeground >> 0x00) & 0xFF;

                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(r / 255f, g / 255f, b / 255f, 1));
                        pushColorCount++;
                        break;
                    case TextPayload t:
                        ImGui.Text($"{t.Text}");
                        ImGui.SameLine();
                        break;
                }
            }
            ImGui.EndGroup();
            if (pushColorCount > 0) ImGui.PopStyleColor(pushColorCount);
        }
    }
}
