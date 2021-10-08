using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public bool ShouldSerializeNotificationToastAdjustments() => NotificationToastAdjustments != null;
        public NotificationToastAdjustments.Configs NotificationToastAdjustments = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class NotificationToastAdjustments : UiAdjustments.SubTweak {
        public override string Name => "Notification Toast Adjustments";
        public override string Description => "Allows moving or hiding of the notifications that appears in the middle of the screen at various times.";
        protected override string Author => "Aireil";

        public class Configs : TweakConfig {
            public bool Hide = false;
            public bool ShowInCombat = false;
            public int OffsetXPosition = 0;
            public int OffsetYPosition = 0;
            public float Scale = 1;
            public readonly List<string> Exceptions = new List<string>();
        }

        public Configs Config { get; private set; }

        private string newException = string.Empty;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Hide", ref Config.Hide);
            if (Config.Hide) {
                ImGui.SameLine();
                hasChanged |= ImGui.Checkbox("Show in combat", ref Config.ShowInCombat);
            }

            if (!Config.Hide || Config.ShowInCombat) {
                var offsetChanged = false;
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                offsetChanged |= ImGui.InputInt("Horizontal Offset##offsetPosition", ref Config.OffsetXPosition, 1);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                offsetChanged |= ImGui.InputInt("Vertical Offset##offsetPosition", ref Config.OffsetYPosition, 1);
                ImGui.SetNextItemWidth(200 * ImGui.GetIO().FontGlobalScale);
                offsetChanged |= ImGui.SliderFloat("##toastScale", ref Config.Scale, 0.1f, 5f, "Toast Scale: %.1fx");
                if (offsetChanged)
                {
                    var toastNode = GetToastNode(2);
                    if (toastNode != null && !toastNode->IsVisible)
                        Service.Toasts.ShowNormal("This is a preview of a toast message.");
                    hasChanged = true;
                }
            }

            if (Config.Hide) return;

            ImGui.Text("Hide toast if text contains:");
            for (var  i = 0; i < Config.Exceptions.Count; i++) {
                ImGui.PushID($"Exception_{i.ToString()}");
                var exception = Config.Exceptions[i];
                if (ImGui.InputText("##ToastTextException", ref exception, 500)) {
                    Config.Exceptions[i] = exception;
                    hasChanged = true;
                }
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString())) {
                    Config.Exceptions.RemoveAt(i--);
                    hasChanged = true;
                }
                ImGui.PopFont();
                ImGui.PopID();
                if (i < 0) break;
            }
            ImGui.InputText("##NewToastTextException", ref newException, 500);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString())) {
                Config.Exceptions.Add(newException);
                newException = string.Empty;
                hasChanged = true;
            }
            ImGui.PopFont();
        };

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.UiAdjustments.NotificationToastAdjustments ?? new Configs();
            Service.Framework.Update += FrameworkOnUpdate;
            Service.Toasts.Toast += OnToast;
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            PluginConfig.UiAdjustments.NotificationToastAdjustments = null;
            Service.Framework.Update -= FrameworkOnUpdate;
            Service.Toasts.Toast -= OnToast;
            UpdateNotificationToast(true);
            base.Disable();
        }

        private void FrameworkOnUpdate(Framework framework) {
            try {
                UpdateNotificationToast();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateNotificationToast(bool reset = false) {
            UpdateNotificationToastText(reset, 1);
            UpdateNotificationToastText(reset, 2);
        }

        private void UpdateNotificationToastText(bool reset, int index) {
            var toastNode = GetToastNode(index);
            if (toastNode == null) return;
            
            if (reset) {
                SetOffsetPosition(toastNode, 0.0f, 0.0f, 1);
                toastNode->SetScale(1, 1);
                return;
            }

            if (!toastNode->IsVisible) return;

            SetOffsetPosition(toastNode, Config.OffsetXPosition, Config.OffsetYPosition, Config.Scale);
            toastNode->SetScale(Config.Scale, Config.Scale);
        }

        // index: 1 - special toast, e.g. BLU active actions set load/save
        //        2 - common toast
        private static AtkResNode* GetToastNode(int index) {
            var toastUnitBase = Common.GetUnitBase("_WideText", index);
            if (toastUnitBase == null) return null;
            if (toastUnitBase->UldManager.NodeList == null || toastUnitBase->UldManager.NodeListCount < 4) return null;

            return toastUnitBase->UldManager.NodeList[0];
        }

        private static void SetOffsetPosition(AtkResNode* node, float offsetX, float offsetY, float scale) {
            // default 1080p values
            var defaultXPos = 448.0f;
            var defaultYPos = 628.0f;
            try {
                defaultXPos = (ImGui.GetIO().DisplaySize.X * 1 / 2) - 512 * scale;
                defaultYPos = (ImGui.GetIO().DisplaySize.Y * 3 / 5) - 20 * scale;
            }
            catch (NullReferenceException) { }

            UiHelper.SetPosition(node, defaultXPos + offsetX, defaultYPos - offsetY);
        }

        private void OnToast(ref SeString message, ref ToastOptions options, ref bool isHandled) {
            try {
                if (isHandled) return;

                if (Config.Hide) {
                    if (Config.ShowInCombat && Service.Condition[ConditionFlag.InCombat])
                        return;
                } else {
                    var messageStr = message.ToString();
                    if (Config.Exceptions.All(x => !messageStr.Contains(x))) return;
                }
                
                isHandled = true;
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
    }
}
