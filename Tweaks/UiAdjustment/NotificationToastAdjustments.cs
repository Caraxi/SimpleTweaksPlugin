using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Internal;
using Dalamud.Game.Internal.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public NotificationToastAdjustments.Configs NotificationToastAdjustments = new NotificationToastAdjustments.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class NotificationToastAdjustments : UiAdjustments.SubTweak {
        public override string Name => "Notification Toast Adjustments";
        public override string Description => "Allows moving or hiding of the notifications that appears in the middle of the screen at various times.";
        protected override string Author => "Aireil";

        public class Configs {
            public bool Hide = false;
            public bool ShowInCombat = false;
            public int OffsetXPosition = 0;
            public int OffsetYPosition = 0;
            public float Scale = 1;
            public readonly List<string> Exceptions = new List<string>();
        }

        public Configs Config => PluginConfig.UiAdjustments.NotificationToastAdjustments;

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
                    var toastNode = GetToastNode();
                    if (toastNode != null && !toastNode->IsVisible)
                        this.PluginInterface.Framework.Gui.Toast.ShowNormal("This is a preview of a toast message.");
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
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnUpdate;
            PluginInterface.Framework.Gui.Toast.OnToast += OnToast;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnUpdate;
            PluginInterface.Framework.Gui.Toast.OnToast -= OnToast;
            UpdateNotificationToastText(true);
            base.Disable();
        }

        private void FrameworkOnUpdate(Framework framework) {
            try {
                UpdateNotificationToastText();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void UpdateNotificationToastText(bool reset = false) {
            var toastNode = GetToastNode();
            if (toastNode == null) return;
            
            if (reset) {
                SetOffsetPosition(toastNode, 0.0f, 0.0f, 1);
                UiHelper.SetScale(toastNode, 1);
                return;
            }

            if (!toastNode->IsVisible) return;

            SetOffsetPosition(toastNode, Config.OffsetXPosition, Config.OffsetYPosition, Config.Scale);
            UiHelper.SetScale(toastNode, Config.Scale);
        }

        private static AtkResNode* GetToastNode() {
            var toastUnitBase = Common.GetUnitBase("_WideText", 2);
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
                    if (Config.ShowInCombat && PluginInterface.ClientState.Condition[Dalamud.Game.ClientState.ConditionFlag.InCombat])
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