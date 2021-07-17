﻿using System;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using SimpleTweaksPlugin.TweakSystem;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;

namespace SimpleTweaksPlugin {
    public partial class UiAdjustmentsConfig {
        public bool ShouldSerializeTargetHP() => TargetHP != null;
        public TargetHP.Configs TargetHP = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class TargetHP : UiAdjustments.SubTweak {
        public class Configs : TweakConfig {
            public DisplayFormat DisplayFormat = DisplayFormat.OneDecimalPrecision;
            public Vector2 Position = new Vector2(0);
            public bool UseCustomColor = false;
            public Vector4 CustomColor = new Vector4(1);
            public byte FontSize = 14;
            
            public bool NoFocus;
            public Vector2 FocusPosition = new Vector2(0);
            public bool FocusUseCustomColor = false;
            public Vector4 FocusCustomColor = new Vector4(1);
            public byte FocusFontSize = 14;
        }
        
        public enum DisplayFormat {
            [Description("Full Number")] 
            FullNumber,
            [Description("Full Number, with Separators (5,555,555)")]
            FullNumberSeparators,
            [Description("Short Number (5K, 5M)")]
            ZeroDecimalPrecision,
            [Description("1 Decimal (5.5K, 5.5M)")]
            OneDecimalPrecision,
            [Description("2 Decimal (5.55K, 5.55M)")]
            TwoDecimalPrecision,
        }
        
        public Configs Config { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            if (ImGui.BeginCombo("Display Format###targetHpFormat", Config.DisplayFormat.GetDescription())) {
                foreach (var v in (DisplayFormat[])Enum.GetValues(typeof(DisplayFormat))) {
                    if (!ImGui.Selectable($"{v.GetDescription()}##targetHpFormatSelect", Config.DisplayFormat == v)) continue;
                    Config.DisplayFormat = v;
                    hasChanged = true;
                }
                ImGui.EndCombo();
            }

            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("X Offset##AdjustTargetHPPositionX", ref Config.Position.X, 1, 5, "%.0f");
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("Y Offset##AdjustTargetHPPositionY", ref Config.Position.Y, 1, 5, "%0.f");
            hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFontSize", ref Config.FontSize);
            hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPUseCustomColor", ref Config.UseCustomColor);
            if (Config.UseCustomColor) {
                ImGui.SameLine();
                hasChanged |= ImGui.ColorEdit4("##TargetHPCustomColor", ref Config.CustomColor);
            }
            
            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.Checkbox("Disable Focus Target HP", ref Config.NoFocus);

            if (!Config.NoFocus) {
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGui.InputFloat("Focus Target X Offset##AdjustTargetHPFocusPositionX", ref Config.FocusPosition.X, 1, 5, "%.0f");
                ImGui.SetNextItemWidth(150);
                hasChanged |= ImGui.InputFloat("Focus Target Y Offset##AdjustTargetHPFocusPositionY", ref Config.FocusPosition.Y, 1, 5, "%0.f");
                hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFocusFontSize", ref Config.FocusFontSize);
                hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPFocusUseCustomColor", ref Config.FocusUseCustomColor);
                if (Config.FocusUseCustomColor) {
                    ImGui.SameLine();
                    hasChanged |= ImGui.ColorEdit4("##TargetHPFocusCustomColor", ref Config.FocusCustomColor);
                }
            }
        };
        
        public override string Name => "Target HP";
        public override string Description => "Displays the exact (or optionally rounded) value of target's hitpoints.";

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.UiAdjustments.TargetHP ?? new Configs();
            PluginInterface.Framework.OnUpdateEvent += FrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            PluginConfig.UiAdjustments.TargetHP = null;
            PluginInterface.Framework.OnUpdateEvent -= FrameworkUpdate;
            Update(true);
            base.Disable();
        }

        private void FrameworkUpdate(Framework framework) {
            try {
                Update();
            } catch(Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private void Update(bool reset = false) {
            var target = PluginInterface.ClientState.Targets.SoftTarget ?? PluginInterface.ClientState.Targets.CurrentTarget;
            if (target != null || reset) {
                var ui = (AtkUnitBase*) PluginInterface?.Framework.Gui.GetUiObjectByName("_TargetInfo", 1);
                if (ui != null && (ui->IsVisible || reset)) {
                    UpdateMainTarget(ui, target, reset);
                }
                
                var splitUi = (AtkUnitBase*) PluginInterface?.Framework.Gui.GetUiObjectByName("_TargetInfoMainTarget", 1);
                if (splitUi != null && (splitUi->IsVisible || reset)) {
                    UpdateMainTargetSplit(splitUi, target, reset);
                }
            }
            
            if (PluginInterface?.ClientState?.Targets?.FocusTarget != null || reset) {
                var ui = (AtkUnitBase*) PluginInterface?.Framework.Gui.GetUiObjectByName("_FocusTargetInfo", 1);
                if (ui != null && (ui->IsVisible || reset)) {
                    UpdateFocusTarget(ui, PluginInterface?.ClientState?.Targets?.FocusTarget, reset);
                }
            }
        }
        
        private void UpdateMainTarget(AtkUnitBase* unitBase, Actor target, bool reset = false) {
            if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 40) return;
            var gauge = (AtkComponentNode*) unitBase->UldManager.NodeList[36];
            var textNode = (AtkTextNode*) unitBase->UldManager.NodeList[39];
            UiHelper.SetSize(unitBase->UldManager.NodeList[37], reset ? 44 : 0, reset ? 20 : 0);
            UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, reset);
        }
        private void UpdateFocusTarget(AtkUnitBase* unitBase, Actor target, bool reset = false) {
            if (Config.NoFocus) reset = true;
            if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 11) return;
            var gauge = (AtkComponentNode*) unitBase->UldManager.NodeList[2];
            var textNode = (AtkTextNode*) unitBase->UldManager.NodeList[10];
            UpdateGaugeBar(gauge, textNode, target, Config.FocusPosition, Config.FocusUseCustomColor ? Config.FocusCustomColor : null, Config.FocusFontSize, reset);
        }
        private void UpdateMainTargetSplit(AtkUnitBase* unitBase, Actor target, bool reset = false) {
            if (unitBase == null || unitBase->UldManager.NodeList == null || unitBase->UldManager.NodeListCount < 9) return;
            var gauge = (AtkComponentNode*) unitBase->UldManager.NodeList[5];
            var textNode = (AtkTextNode*) unitBase->UldManager.NodeList[8];
            UiHelper.SetSize(unitBase->UldManager.NodeList[6], reset ? 44 : 0, reset ? 20 : 0);
            UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, reset);
        }

        private const int TargetHPNodeID = 99990001;
        
        private void UpdateGaugeBar(AtkComponentNode* gauge, AtkTextNode* cloneTextNode, Actor target, Vector2 positionOffset, Vector4? customColor, byte fontSize, bool reset = false) {
            if (gauge == null || (ushort) gauge->AtkResNode.Type < 1000) return;
            
            AtkTextNode* textNode = null;

            for (var i = 5; i < gauge->Component->UldManager.NodeListCount; i++) {
                var node = gauge->Component->UldManager.NodeList[i];
                if (node->Type == NodeType.Text && node->NodeID == TargetHPNodeID) {
                    textNode = (AtkTextNode*) node;
                    break;
                }
            }

            if (textNode == null && reset) return; // Nothing to clean
            
            if (textNode == null) {
                textNode = UiHelper.CloneNode(cloneTextNode);
                textNode->AtkResNode.NodeID = TargetHPNodeID;
                var newStrPtr = Common.Alloc(512);
                textNode->NodeText.StringPtr = (byte*) newStrPtr;
                textNode->NodeText.BufSize = 512;
                UiHelper.SetText(textNode, "");
                UiHelper.ExpandNodeList(gauge, 1);
                gauge->Component->UldManager.NodeList[gauge->Component->UldManager.NodeListCount++] = (AtkResNode*) textNode;

                var nextNode = gauge->Component->UldManager.RootNode;
                while (nextNode->PrevSiblingNode != null) {
                    nextNode = nextNode->PrevSiblingNode;
                }
                
                textNode->AtkResNode.ParentNode = (AtkResNode*) gauge;
                textNode->AtkResNode.ChildNode = null;
                textNode->AtkResNode.PrevSiblingNode = null;
                textNode->AtkResNode.NextSiblingNode = nextNode;
                nextNode->PrevSiblingNode = (AtkResNode*) textNode;
            }

            if (reset) {
                UiHelper.Hide(textNode);
                return;
            }

            textNode->AlignmentFontType = (byte)AlignmentType.BottomRight;
            
            UiHelper.SetPosition(textNode, positionOffset.X, positionOffset.Y);
            UiHelper.SetSize(textNode, gauge->AtkResNode.Width - 5, gauge->AtkResNode.Height);
            UiHelper.Show(textNode);
            if (!customColor.HasValue) {
                textNode->TextColor = cloneTextNode->TextColor;
            } else {
                textNode->TextColor.A = (byte) (customColor.Value.W * 255);
                textNode->TextColor.R = (byte) (customColor.Value.X * 255);
                textNode->TextColor.G = (byte) (customColor.Value.Y * 255);
                textNode->TextColor.B = (byte) (customColor.Value.Z * 255);
            }
            textNode->EdgeColor = cloneTextNode->EdgeColor;
            textNode->FontSize = fontSize;
            
            
            if (target is Chara chara) {
                UiHelper.SetText(textNode, $"{FormatNumber(chara.CurrentHp)}/{FormatNumber(chara.MaxHp)}");
            } else {
                UiHelper.SetText(textNode, "");
            }
        }

        private string FormatNumber(uint num) {
            if (Config.DisplayFormat == DisplayFormat.FullNumber) return $"{num}";
            if (Config.DisplayFormat == DisplayFormat.FullNumberSeparators) return $"{num:N0}";

            var fStr = Config.DisplayFormat switch {
                DisplayFormat.OneDecimalPrecision => "F1",
                DisplayFormat.TwoDecimalPrecision => "F2",
                _ => "F0"
            };

            return num switch {
                >= 1000000 => $"{(num / 1000000f).ToString(fStr)}M",
                >= 1000 => $"{(num / 1000f).ToString(fStr)}K",
                _ => $"{num}"
            };
        }
    }
}
