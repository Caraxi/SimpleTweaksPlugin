using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Internal;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ParameterBarAdjustments : UiAdjustments.SubTweak {
        public override string Name => "Parameter Bar Adjustments";
        public override string Description => "Allows hiding or moving specific parts of the parameter bar (HP and mana bars).";
        protected override string Author => "Aireil";
        public override IEnumerable<string> Tags => new[] {"parameter", "hp", "mana", "bar"};

        public class Configs : TweakConfig
        {
            public bool HideTargetCycling = false;
            public int TargetCyclingOffsetX = DefaultValues.TargetCyclingOffsetX;
            public int TargetCyclingOffsetY = DefaultValues.TargetCyclingOffsetY;

            public bool HideManaBar = false;
            public bool HideManaValue = false;
            public bool HideManaTitle = false;
            public int ManaBarOffsetX = DefaultValues.ManaBarOffsetX;
            public int ManaBarOffsetY = DefaultValues.BarOffsetY;
            public int ManaValueOffsetX = DefaultValues.ValueOffsetX;
            public int ManaValueOffsetY = DefaultValues.ValueOffsetY;

            public bool HideHpBar = false;
            public bool HideHpValueText = false;
            public bool HideHpTitle = false;
            public int HpBarOffsetX = DefaultValues.HpBarOffsetX;
            public int HpBarOffsetY = DefaultValues.BarOffsetY;
            public int HpValueOffsetX = DefaultValues.ValueOffsetX;
            public int HpValueOffsetY = DefaultValues.ValueOffsetY;
        }

        private static class DefaultValues
        {
            public const int TargetCyclingOffsetX = 100;
            public const int TargetCyclingOffsetY = 1;

            public const int ManaBarOffsetX = 256;
            public const int HpBarOffsetX = 96;


            public const int BarOffsetY = 12;
            public const int ValueOffsetX = 24;
            public const int ValueOffsetY = 7;
        }

        public Configs Config { get; private set; }

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            UpdateParameterBar(true);
            SaveConfig(Config);
            base.Disable();
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
                UpdateParameterBar();
            }
            catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
        {
            var positionOffset = 185 * ImGui.GetIO().FontGlobalScale;
            var resetOffset = 250 * ImGui.GetIO().FontGlobalScale;
            hasChanged |= ImGui.Checkbox("Hide Target Cycling", ref Config.HideTargetCycling);
            if (!Config.HideTargetCycling) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("##offsetTargetCyclingOffsetX", ref Config.TargetCyclingOffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset##offsetTargetCyclingOffsetY", ref Config.TargetCyclingOffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##offsetTargetCyclingOffset")) {
                    Config.TargetCyclingOffsetX = DefaultValues.TargetCyclingOffsetX;
                    Config.TargetCyclingOffsetY = DefaultValues.TargetCyclingOffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);

            hasChanged |= ImGui.Checkbox("Hide HP Bar", ref Config.HideHpBar);
            if (!Config.HideHpBar) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("##offsetHpBarOffsetX", ref Config.HpBarOffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset##offsetHpBarOffsetY", ref Config.HpBarOffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##offsetHpBarOffset")) {
                    Config.HpBarOffsetX = DefaultValues.HpBarOffsetX;
                    Config.HpBarOffsetY = DefaultValues.BarOffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }
            hasChanged |= ImGui.Checkbox("Hide 'HP' Text", ref Config.HideHpTitle);
            hasChanged |= ImGui.Checkbox("Hide HP Value", ref Config.HideHpValueText);
            if (!Config.HideHpValueText) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("##offsetHpValueOffsetX", ref Config.HpValueOffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset from the HP bar##offsetHpValueOffsetY", ref Config.HpValueOffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##offsetHpValueOffset")) {
                    Config.HpValueOffsetX = DefaultValues.ValueOffsetX;
                    Config.HpValueOffsetY = DefaultValues.ValueOffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }

            ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);

            hasChanged |= ImGui.Checkbox("Hide Mana Bar", ref Config.HideManaBar);
            if (!Config.HideManaBar) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("##offsetManaBarOffsetX", ref Config.ManaBarOffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset##offsetManaBarOffsetY", ref Config.ManaBarOffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##offsetManaBarOffset")) {
                    Config.ManaBarOffsetX = DefaultValues.ManaBarOffsetX;
                    Config.ManaBarOffsetY = DefaultValues.BarOffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }
            hasChanged |= ImGui.Checkbox("Hide 'MP' Text", ref Config.HideManaTitle);
            hasChanged |= ImGui.Checkbox("Hide Mana Value", ref Config.HideManaValue);
            if (!Config.HideManaValue) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset);
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("##offsetManaValueOffsetX", ref Config.ManaValueOffsetX);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
                ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
                hasChanged |= ImGui.InputInt("Offset from the mana bar##offsetManaValueOffsetY", ref Config.ManaValueOffsetY);
                ImGui.SameLine();
                ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##offsetManaValueOffset")) {
                    Config.ManaValueOffsetX = DefaultValues.ValueOffsetX;
                    Config.ManaValueOffsetY = DefaultValues.ValueOffsetY;
                    hasChanged = true;
                }
                ImGui.PopFont();
            }


            if (hasChanged) {
                UpdateParameterBar(true);
            }
        };

        private void UpdateParameterBar(bool reset = false)
        {
            var parameterWidgetUnitBase = Common.GetUnitBase("_ParameterWidget");
            if (parameterWidgetUnitBase == null) return;

            // Target cycling
            var targetCyclingNode = parameterWidgetUnitBase->UldManager.SearchNodeById(2);
            targetCyclingNode->SetPositionFloat(Config.TargetCyclingOffsetX, Config.TargetCyclingOffsetY);
            if (Config.HideTargetCycling) targetCyclingNode->Color.A = 0;

            // Mana
            var manaNode = (AtkComponentNode*) parameterWidgetUnitBase->UldManager.SearchNodeById(4);
            if (manaNode == null) return;
            var manaValueNode = manaNode->Component->UldManager.SearchNodeById(3);
            var manaTitleNode = manaNode->Component->UldManager.SearchNodeById(2);
            var manaTextureNode = manaNode->Component->UldManager.SearchNodeById(8);
            var manaTexture2Node = manaNode->Component->UldManager.SearchNodeById(4);
            var manaNineGridNode = manaNode->Component->UldManager.SearchNodeById(7);
            var manaNineGrid2Node = manaNode->Component->UldManager.SearchNodeById(6);
            var manaNineGrid3Node= manaNode->Component->UldManager.SearchNodeById(5);

            manaNode->AtkResNode.SetPositionFloat(Config.ManaBarOffsetX, Config.ManaBarOffsetY);
            manaValueNode->SetPositionFloat(Config.ManaValueOffsetX, Config.ManaValueOffsetY);
            if (Config.HideManaValue) manaValueNode->Color.A = 0;
            if (Config.HideManaTitle) manaTitleNode->Color.A = 0;

            if (Config.HideManaBar)
            {
                manaNineGridNode->Color.A = 0;
                manaNineGrid2Node->Color.A = 0;
                manaNineGrid3Node->Color.A = 0;
                manaTextureNode->Color.A = 0;
                manaTexture2Node->Color.A = 0;
            }

            // HP
            var hpNode = (AtkComponentNode*) parameterWidgetUnitBase->UldManager.SearchNodeById(3);
            if (hpNode == null) return;
            var hpValueNode = hpNode->Component->UldManager.SearchNodeById(3);
            var hpTitleNode = hpNode->Component->UldManager.SearchNodeById(2);
            var hpTextureNode = hpNode->Component->UldManager.SearchNodeById(8);
            var hpTexture2Node = hpNode->Component->UldManager.SearchNodeById(4);
            var hpNineGridNode = hpNode->Component->UldManager.SearchNodeById(7);
            var hpNineGrid2Node = hpNode->Component->UldManager.SearchNodeById(6);
            var hpNineGrid3Node= hpNode->Component->UldManager.SearchNodeById(5);

            hpNode->AtkResNode.SetPositionFloat(Config.HpBarOffsetX, Config.HpBarOffsetY);
            hpValueNode->SetPositionFloat(Config.HpValueOffsetX, Config.HpValueOffsetY);
            if (Config.HideHpValueText) hpValueNode->Color.A = 0;
            if (Config.HideHpTitle) hpTitleNode->Color.A = 0;

            if (Config.HideHpBar)
            {
                hpNineGridNode->Color.A = 0;
                hpNineGrid2Node->Color.A = 0;
                hpNineGrid3Node->Color.A = 0;
                hpTextureNode->Color.A = 0;
                hpTexture2Node->Color.A = 0;
            }

            if (reset) {
                // Target cycling
                targetCyclingNode->Color.A = 255;
                targetCyclingNode->SetPositionFloat(DefaultValues.TargetCyclingOffsetX, DefaultValues.TargetCyclingOffsetY);

                // Mana
                manaNode->AtkResNode.SetPositionFloat(DefaultValues.ManaBarOffsetX, DefaultValues.BarOffsetY);
                manaValueNode->SetPositionFloat(DefaultValues.ValueOffsetX, DefaultValues.ValueOffsetY);

                manaNineGridNode->Color.A = 255;
                manaNineGrid2Node->Color.A = 255;
                manaNineGrid3Node->Color.A = 255;
                manaTextureNode->Color.A = 255;
                manaTexture2Node->Color.A = 255;

                manaTitleNode->Color.A = 255;
                manaValueNode->Color.A = 255;

                // HP
                hpNode->AtkResNode.SetPositionFloat(DefaultValues.HpBarOffsetX, DefaultValues.BarOffsetY);
                hpValueNode->SetPositionFloat(DefaultValues.ValueOffsetX, DefaultValues.ValueOffsetY);

                hpNineGridNode->Color.A = 255;
                hpNineGrid2Node->Color.A = 255;
                hpNineGrid3Node->Color.A = 255;
                hpTextureNode->Color.A = 255;
                hpTexture2Node->Color.A = 255;

                hpTitleNode->Color.A = 255;
                hpValueNode->Color.A = 255;
            }
        }
    }
}
