using System;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public DisableChatAutoscroll.Configs DisableChatAutoscroll = new();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class DisableChatAutoscroll : UiAdjustments.SubTweak {
        public override string Name => "Disable Chat AutoScroll";
        public class Configs {
            public bool DisablePanel0;
            public bool DisablePanel1;
            public bool DisablePanel2;
            public bool DisablePanel3;
        }

        public Configs Config => PluginConfig.UiAdjustments.DisableChatAutoscroll;
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.Text("Allow autoscrolling in:");
            ImGui.Indent();
            hasChanged |= ImGui.Checkbox("Tab 1##allowAutoscroll", ref Config.DisablePanel0);
            hasChanged |= ImGui.Checkbox("Tab 2##allowAutoscroll", ref Config.DisablePanel1);
            hasChanged |= ImGui.Checkbox("Tab 3##allowAutoscroll", ref Config.DisablePanel2);
            hasChanged |= ImGui.Checkbox("Tab 4##allowAutoscroll", ref Config.DisablePanel3);
            ImGui.Unindent();
        };
        
        private delegate void* ScrollToBottomDelegate(void* a1);

        private HookWrapper<ScrollToBottomDelegate> scrollToBottomHook;
        
        public override void Enable() {
            scrollToBottomHook = Common.Hook<ScrollToBottomDelegate>("E8 ?? ?? ?? ?? 48 85 FF 75 0D", ScrollToBottomDetour);
            base.Enable();
        }

        private void* ScrollToBottomDetour(void* a1) {
            try {
                var panel = (AddonChatLogPanel*) ((ulong) a1 - 0x268);
                var name = Marshal.PtrToStringAnsi(new IntPtr(panel->AtkUnitBase.Name));
                if (!string.IsNullOrEmpty(name)) {
                    var isEnabled = name switch {
                        "ChatLogPanel_0" => !Config.DisablePanel0,
                        "ChatLogPanel_1" => !Config.DisablePanel1,
                        "ChatLogPanel_2" => !Config.DisablePanel2,
                        "ChatLogPanel_3" => !Config.DisablePanel3,
                        _ => false
                    };

                    if (isEnabled && panel->IsScrolledBottom == 0) {
                        SimpleLog.Verbose($"Prevented Autoscroll in: {name}");
                        return null;
                    }
                }
                return scrollToBottomHook.Original(a1);
            } catch {
                return scrollToBottomHook.Original(a1);
            }
        }

        public override void Disable() {
            scrollToBottomHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            scrollToBottomHook?.Dispose();
            base.Dispose();
        }

    }
}
