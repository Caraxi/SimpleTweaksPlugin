using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;
using HotBarSlot = FFXIVClientStructs.FFXIV.Client.UI.Misc.HotBarSlot;
using HotbarSlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.HotbarSlotType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class LargeCooldownCounter : UiAdjustments.SubTweak {

        private delegate byte ActionBarBaseUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);

        private HookWrapper<ActionBarBaseUpdate> actionBarBaseUpdateHook;

        public override string Name => "Large Cooldown Counter";
        public override string Description => "Increases the size of cooldown counters on hotbars.";

        public override void Enable() {
            actionBarBaseUpdateHook ??= Common.Hook<ActionBarBaseUpdate>("E8 ?? ?? ?? ?? 83 BB ?? ?? ?? ?? ?? 75 09", ActionBarBaseUpdateDetour);
            Config = LoadConfig<Configs>() ?? new Configs();
            actionBarBaseUpdateHook?.Enable();
            base.Enable();
        }

        private byte ActionBarBaseUpdateDetour(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
            var ret = actionBarBaseUpdateHook.Original(atkUnitBase, numberArrayData, stringArrayData);
            try {
                UpdateAll();
            } catch {
                //
            }
            return ret;
        }

        private readonly string[] allActionBars = {
            "_ActionBar",
            "_ActionBar01",
            "_ActionBar02",
            "_ActionBar03",
            "_ActionBar04",
            "_ActionBar05",
            "_ActionBar06",
            "_ActionBar07",
            "_ActionBar08",
            "_ActionBar09",
            "_ActionCross",
            "_ActionDoubleCrossL",
            "_ActionDoubleCrossR",
        };
        public class Configs : TweakConfig {
            public Font Font = Font.Default;
            public int FontSizeAdjust;
            public bool SimpleMode;
            public Vector4 CooldownColour = new(1, 1, 1, 1);
            public Vector4 CooldownEdgeColour = new(0.2F, 0.2F, 0.2F, 1);
        }

        public Configs Config { get; private set; }
        
        public enum Font {
            Default,
            FontB,
            FontC,
            FontD,
        }
        
        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.BeginCombo(LocString("Font") + "###st_uiAdjustment_largeCooldownCounter_fontSelect", $"{Config.Font}")) {
                foreach (var f in (Font[])Enum.GetValues(typeof(Font))) {
                    if (ImGui.Selectable($"{f}##st_uiAdjustment_largeCooldownCount_fontOption", f == Config.Font)) {
                        Config.Font = f;
                        hasChanged = true;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SetNextItemWidth(160 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.SliderInt(LocString("Font Size Adjust") + "##st_uiAdjustment_largEcooldownCounter_fontSize", ref Config.FontSizeAdjust, -15, 30);
            hasChanged |= ImGui.Checkbox(LocString("Simple Mode") + "##st_uiAdjustment_largeCooldownCounter_simpleMode", ref Config.SimpleMode);
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text(LocString("Simple Mode"));
                ImGui.Separator();
                ImGui.Text(LocString("SimpleModeDescription", "Reverts to old cooldown checking.\nFixes issues with XIVCombo.\nHas some issues when out of range.\nCannot change colour of text with Simple Mode enabled."));
                ImGui.EndTooltip();
            }

            if (!Config.SimpleMode) {
                hasChanged |= ImGui.ColorEdit4(LocString("Text Colour") + "##largeCooldownCounter", ref Config.CooldownColour);
                hasChanged |= ImGui.ColorEdit4(LocString("Edge Colour") + "##largeCooldownCounter", ref Config.CooldownEdgeColour);
            }

        };

        private void UpdateAll(bool reset = false) {
            foreach (var actionBar in allActionBars) {
                var ab = (AddonActionBarBase*) Service.GameGui.GetAddonByName(actionBar, 1);
                Update(ab, reset);
            }
        }

        private void Update(AddonActionBarBase* ab, bool reset = false) {
            if (ab == null || ab->ActionBarSlotsAction == null) return;
            var hotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            var name = Marshal.PtrToStringUTF8(new IntPtr(ab->AtkUnitBase.Name));
            if (name == null) return;

            for (var i = 0; i < ab->HotbarSlotCount; i++) {
                var slot = ab->ActionBarSlotsAction[i];

                var slotStruct = hotbarModule->HotBar[ab->HotbarID]->Slot[i];

                if (name.StartsWith("_ActionDoubleCross")) {
                    var dcBar = (AddonActionDoubleCrossBase*)ab;
                    slotStruct = hotbarModule->HotBar[dcBar->BarTarget]->Slot[i + (dcBar->UseLeftSide != 0 ? 0 : 8)];
                }

                if ((slot.PopUpHelpTextPtr != null || reset) && slot.Icon != null) {
                    UpdateIcon(slot.Icon, slotStruct, reset);
                }
            }
        }

        private byte DefaultFontSize => Config.Font switch {
            Font.FontB => 14,
            Font.FontC => 15,
            Font.FontD => 34,
            _ => 18,
        };

        private byte GetFontSize() {
            var s = (Config.FontSizeAdjust * 2) + DefaultFontSize;
            if (s < 4) s = 4;
            if (s > 255) s = 255;
            return (byte) s;
        }
        
        private void UpdateIcon(AtkComponentNode* iconComponent, HotBarSlot* slotStruct, bool reset = false) {
            if (iconComponent == null) return;
            var cooldownTextNode = (AtkTextNode*)iconComponent->Component->UldManager.NodeList[13];
            if (cooldownTextNode->AtkResNode.Type != NodeType.Text) return;
            if (reset == false && (cooldownTextNode->AtkResNode.Flags & 0x10) != 0x10) return;
            if (cooldownTextNode == null) return;
            if (!Config.SimpleMode && slotStruct != null && slotStruct->CommandType == HotbarSlotType.Action) {
                var adjustedActionId = SimpleTweaksPlugin.Client.ActionManager.GetAdjustedActionId(slotStruct->CommandId);
                var recastGroup = (int) SimpleTweaksPlugin.Client.ActionManager.GetRecastGroup((byte)slotStruct->CommandType, adjustedActionId) + 1;
                if (recastGroup == 0 || recastGroup == 58) {
                    reset = true;
                } else {
                    var recastTimer = SimpleTweaksPlugin.Client.ActionManager.GetGroupRecastTime(recastGroup);
                    if (recastTimer->IsActive == 0) reset = true;
                }
            } else {
                if (cooldownTextNode->EdgeColor.R != 0x33) reset = true;
            }

            if (reset) {
                cooldownTextNode->AtkResNode.X = 3;
                cooldownTextNode->AtkResNode.Y = 37;
                cooldownTextNode->AtkResNode.Width = 48;
                cooldownTextNode->AtkResNode.Height = 12;
                cooldownTextNode->AlignmentFontType = (byte)AlignmentType.Left;
                cooldownTextNode->FontSize = 12;
            } else {
                cooldownTextNode->AtkResNode.X = 0;
                cooldownTextNode->AtkResNode.Y = 0;
                cooldownTextNode->AtkResNode.Width = 46;
                cooldownTextNode->AtkResNode.Height = 46;
                cooldownTextNode->AlignmentFontType = (byte)((0x10 * (byte) Config.Font) | (byte) AlignmentType.Center);
                cooldownTextNode->FontSize = GetFontSize();

                if (!Config.SimpleMode) {
                    cooldownTextNode->TextColor.R = (byte)(Config.CooldownColour.X * 255f);
                    cooldownTextNode->TextColor.G = (byte)(Config.CooldownColour.Y * 255f);
                    cooldownTextNode->TextColor.B = (byte)(Config.CooldownColour.Z * 255f);
                    cooldownTextNode->TextColor.A = (byte)(Config.CooldownColour.W * 255f);

                    cooldownTextNode->EdgeColor.R = (byte)(Config.CooldownEdgeColour.X * 255f);
                    cooldownTextNode->EdgeColor.G = (byte)(Config.CooldownEdgeColour.Y * 255f);
                    cooldownTextNode->EdgeColor.B = (byte)(Config.CooldownEdgeColour.Z * 255f);
                    cooldownTextNode->EdgeColor.A = (byte)(Config.CooldownEdgeColour.W * 255f);
                }
            }
            
            cooldownTextNode->AtkResNode.Flags_2 |= 0x1;
        }

        public override void Disable() {
            actionBarBaseUpdateHook?.Disable();
            SaveConfig(Config);
            UpdateAll(true);
            base.Disable();
        }

        public override void Dispose() {
            actionBarBaseUpdateHook?.Dispose();
            base.Dispose();
        }
    }
}
