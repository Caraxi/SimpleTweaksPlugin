using System;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class AutoOpenLootWindow : Tweak {
        public override string Name => "Open loot window when items are added.";

        public override string Description => "Open the loot rolling window when new items are added to be rolled on.";

        public override void Enable() {
            Service.Chat.CheckMessageHandled += HandleChat;
            base.Enable();
        }

        private void HandleChat(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            try {
                if ((ushort)type != 2105) return;
                if (message.TextValue == Service.ClientState.ClientLanguage switch
                {
                    ClientLanguage.German => "Bitte um das Beutegut würfeln.",
                    ClientLanguage.French => "Veuillez lancer les dés pour le butin.",
                    ClientLanguage.Japanese => "ロットを行ってください。",
                    _ => "Cast your lot."
                })
                {
                    if (Service.Condition[ConditionFlag.WatchingCutscene]
                        || Service.Condition[ConditionFlag.WatchingCutscene78]
                        || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent])
                    {
                        Service.Framework.Update -= TryOpenAfterCutsceneFrameworkUpdate;
                        Service.Framework.Update += TryOpenAfterCutsceneFrameworkUpdate;
                    }
                    else
                    {
                        TryOpenWindow();
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private byte throttle;
        private void TryOpenAfterCutsceneFrameworkUpdate(Framework framework)
        {
            throttle++;
            if (throttle <= 10) return;
            throttle = 0;
            if (Service.Condition[ConditionFlag.WatchingCutscene]
                || Service.Condition[ConditionFlag.WatchingCutscene78]
                || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent])
            {
                return;
            }
            Service.Framework.Update -= TryOpenAfterCutsceneFrameworkUpdate;
            TryOpenWindow();
        }

        private static void TryOpenWindow() {
            SimpleLog.Verbose("Try opening NeedGreed");
            var needGreedWindow = Service.GameGui.GetAddonByName("NeedGreed", 1);
            if (needGreedWindow != IntPtr.Zero) {
                SimpleLog.Verbose("NeedGreed already open.");
                return;
            }

            SimpleLog.Verbose("Opening NeedGreed window.");
            var notification = (AtkUnitBase*)Service.GameGui.GetAddonByName("_Notification", 1);
            if (notification== null) {
                SimpleLog.Verbose("_Notification not open.");
                return;
            }

            Common.GenerateCallback(notification, 0, 2);
        }

        public override void Disable() {
            Service.Chat.CheckMessageHandled -= HandleChat;
            Service.Framework.Update -= TryOpenAfterCutsceneFrameworkUpdate;
            base.Disable();
        }
    }
}
