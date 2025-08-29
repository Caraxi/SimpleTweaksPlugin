using System;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    [TweakName("Reposition Target Castbar Text")]
    [TweakDescription("Moves the text on target castbars to make it easier to read")]
    public unsafe class ShiftTargetCastBarText : UiAdjustments.SubTweak {

        public class Config : TweakConfig {
            public int Offset = 8;
            public Alignment NameAlignment = Alignment.BottomRight;
        }
        
        public Config LoadedConfig { get; private set; }

        
        private readonly Vector2 buttonSize = new Vector2(26, 22);

        protected void DrawConfig(ref bool changed)
        {
            {
                var bSize = buttonSize * ImGui.GetIO().FontGlobalScale;
                ImGui.SetNextItemWidth(90 * ImGui.GetIO().FontGlobalScale);
                if (ImGui.InputInt($"###{GetType().Name}_Offset", ref LoadedConfig.Offset))
                {
                    if (LoadedConfig.Offset > MaxOffset) LoadedConfig.Offset = MaxOffset;
                    if (LoadedConfig.Offset < MinOffset) LoadedConfig.Offset = MinOffset;
                    changed = true;
                }

                ImGui.SameLine();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2));
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowUp}", bSize))
                {
                    LoadedConfig.Offset = 8;
                    changed = true;
                }

                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Above progress bar");

                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char)FontAwesomeIcon.CircleNotch}", bSize))
                {
                    LoadedConfig.Offset = 24;
                    changed = true;
                }

                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Original Position");


                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{(char)FontAwesomeIcon.ArrowDown}", bSize))
                {
                    LoadedConfig.Offset = 32;
                    changed = true;
                }

                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Below progress bar");
                ImGui.PopStyleVar();
                ImGui.SameLine();
                ImGui.Text("Ability name vertical offset");

                changed |= ImGuiExt.HorizontalAlignmentSelector("Ability Name Alignment",
                    ref LoadedConfig.NameAlignment, VerticalAlignment.Bottom);
            }
        }


        [FrameworkUpdate]
        public void OnFrameworkUpdate() {
            try {
                HandleBars();
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }

        private void HandleBars(bool reset = false) {
            var focusTargetInfo = Common.GetUnitBase("_FocusTargetInfo");
            if (focusTargetInfo != null && focusTargetInfo->UldManager.NodeList != null && focusTargetInfo->UldManager.NodeListCount > 16 && (focusTargetInfo->IsVisible || reset)) {
                DoShift(focusTargetInfo->GetTextNodeById(5));
            }

            var splitCastBar = Common.GetUnitBase("_TargetInfoCastBar");
            if (splitCastBar != null && splitCastBar->UldManager.NodeList != null && splitCastBar->UldManager.NodeListCount > 5 && (splitCastBar->IsVisible || reset)) {
                DoShift(splitCastBar->GetTextNodeById(4));
                if (!reset) return;
            }

            var mainTargetInfo = Common.GetUnitBase("_TargetInfo");
            if (mainTargetInfo != null && mainTargetInfo->UldManager.NodeList != null && mainTargetInfo->UldManager.NodeListCount > 44 && (mainTargetInfo->IsVisible || reset)) {
                DoShift(mainTargetInfo->GetTextNodeById(12));
            }
        }
        
        private const int MinOffset = 0;
        private const int MaxOffset = 48;

        private void DoShift(AtkTextNode* node, bool reset = false) {
            if (node == null) return;
            var p = LoadedConfig.Offset;
            if (p < MinOffset) p = MinOffset;
            if (p > MaxOffset) p = MaxOffset;
            node->Height = reset ? (ushort) 24 : (ushort) p;
            node->AlignmentFontType = reset ? (byte) AlignmentType.BottomRight : (byte) LoadedConfig.NameAlignment;
            if (reset) {
                UiHelper.SetPosition(node, 0, null);
                UiHelper.SetSize(node, 197, null);
            } else {
                UiHelper.SetPosition(node, 8, null);
                UiHelper.SetSize(node, 188, null);
            }
        }

        protected override void Enable() {
            if (Enabled) return;
            LoadedConfig = LoadConfig<Config>() ?? new Config();
            Enabled = true;
        }

        protected override void Disable() {
            if (!Enabled) return;
            SaveConfig(LoadedConfig);
            SimpleLog.Debug($"[{GetType().Name}] Reset");
            HandleBars(true);
            Enabled = false;
        }

        public override void Dispose() {
            Enabled = false;
            Ready = false;
        }
    }
}
