﻿using System;
using System.Runtime.InteropServices;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.Chat;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin {
    public partial class ChatTweaksConfig {
        public bool ShouldSerializeDisableChatAutoscroll() => DisableChatAutoscroll != null;
        public DisableChatAutoscroll.Configs DisableChatAutoscroll = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Chat {
    public unsafe class DisableChatAutoscroll : ChatTweaks.SubTweak {
        public override string Name => "Smart AutoScroll";
        public override string Description => "Attempts to prevent autoscrolling when receiving new chat messages while scrolled up.";

        public class Configs : TweakConfig {
            public bool DisablePanel0;
            public bool DisablePanel1;
            public bool DisablePanel2;
            public bool DisablePanel3;
        }

        public Configs Config { get; private set; }
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.Text(LocString("AllowAutoscrollLabel", "Always allow autoscrolling in:"));
            ImGui.Indent();
            hasChanged |= ImGui.Checkbox(LocString("TabLabel", "Tab {0}").Format(1) + "##allowAutoscroll", ref Config.DisablePanel0);
            hasChanged |= ImGui.Checkbox(LocString("TabLabel", "Tab {0}").Format(2) + "##allowAutoscroll", ref Config.DisablePanel1);
            hasChanged |= ImGui.Checkbox(LocString("TabLabel", "Tab {0}").Format(3) + "##allowAutoscroll", ref Config.DisablePanel2);
            hasChanged |= ImGui.Checkbox(LocString("TabLabel", "Tab {0}").Format(4) + "##allowAutoscroll", ref Config.DisablePanel3);
            ImGui.Unindent();
        };
        
        private delegate void* ScrollToBottomDelegate(void* a1);

        private HookWrapper<ScrollToBottomDelegate> scrollToBottomHook;

        protected override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.ChatTweaks.DisableChatAutoscroll ?? new Configs();
            scrollToBottomHook = Common.Hook<ScrollToBottomDelegate>("E8 ?? ?? ?? ?? 48 85 FF 75 0D", ScrollToBottomDetour);
            scrollToBottomHook?.Enable();
            base.Enable();
        }

        private void* ScrollToBottomDetour(void* a1) {
            try {
                var panel = (AddonChatLogPanel*) ((ulong) a1 - 0x268);
                var name = panel->NameString;
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

        protected override void Disable() {
            SaveConfig(Config);
            PluginConfig.ChatTweaks.DisableChatAutoscroll = null;
            scrollToBottomHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            scrollToBottomHook?.Dispose();
            base.Dispose();
        }

    }
}
