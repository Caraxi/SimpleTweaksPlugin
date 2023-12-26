using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Reposition Cutscene Dialogue Box")]
[TweakDescription("Allows setting a custom position for dialogue boxes used in cutscenes.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.5.0")]
public unsafe class MoveCutsceneTalk : Tweak {
    public class Config : TweakConfig {
        public AddonConfig AddonTalkConfig = new();
    }

    public class AddonConfig {
        public Vector2 Position = new(0, 0);
        public PositionKind HorizontalPositionKind = PositionKind.FromCentre;
        public PositionKind VerticalPositionKind = PositionKind.FromMax;
    }
    
    public enum PositionKind {
        FromMin,
        FromCentre,
        FromMax
    }
    
    public Config TweakConfig { get; private set; }

    protected override void Enable() {
        if (Common.GetUnitBase("Talk", out var atkUnitBase)) {
            UpdateAddonPosition(atkUnitBase);
        }
    }

    protected override void Disable() {
        if (Common.GetUnitBase("Talk", out var atkUnitBase)) {
            UpdateAddonPosition(atkUnitBase, new Config().AddonTalkConfig);
        }
    }

    [AddonEvent("Talk", AddonEvent.PostSetup, AddonEvent.PostRefresh, AddonEvent.PostRequestedUpdate)]
    private void UpdateAddonPosition(AtkUnitBase* addon) => UpdateAddonPosition(addon, TweakConfig.AddonTalkConfig);

    private void UpdateAddonPosition(AtkUnitBase* addon, AddonConfig addonConfig) {
        // Only change the position of cutscene Talk, others are movable manually
        if (!Service.Condition.Any(ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78, ConditionFlag.OccupiedInCutSceneEvent)) return;
        
        var position = new Vector2(
            addonConfig.HorizontalPositionKind switch {
                PositionKind.FromMin => addonConfig.Position.X,
                PositionKind.FromCentre => Device.Instance()->Width / 2f - addon->GetScaledWidth(true) / 2f + addonConfig.Position.X,
                PositionKind.FromMax => Device.Instance()->Width - addon->GetScaledWidth(true) + addonConfig.Position.X,
                _ => 0
            },
            addonConfig.VerticalPositionKind switch {
                PositionKind.FromMin => addonConfig.Position.Y,
                PositionKind.FromCentre => Device.Instance()->Height / 2f - addon->GetScaledHeight(true) / 2f + addonConfig.Position.Y,
                PositionKind.FromMax => Device.Instance()->Height - addon->GetScaledHeight(true) + addonConfig.Position.Y,
                _ => 0
            }
        );
        
        SimpleLog.Verbose($"Update {Common.ReadString(addon->Name)} Position: [{addon->X}, {addon->Y}] -> [{(short)position.X}, {(short)position.Y}]");
        addon->SetPosition((short)position.X, (short)position.Y);
    }

    private readonly Dictionary<PositionKind, string> horizontalPositionKindLabels = new() {
        [PositionKind.FromMin] = "From Left Side",
        [PositionKind.FromCentre] = "From Center",
        [PositionKind.FromMax] = "From Right Side",
    };
    
    private readonly Dictionary<PositionKind, string> verticalPositionKindLabels = new() {
        [PositionKind.FromMin] = "From Top",
        [PositionKind.FromCentre] = "From Center",
        [PositionKind.FromMax] = "From Bottom",
    };
    
    
    private void DrawConfig() {
        var anyChange = false;
        
        void PositionEditor(string label, ref float offset, ref PositionKind positionKind, Dictionary<PositionKind, string> labels, uint max) {
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            anyChange |= ImGui.DragFloat($"##position{label}", ref offset);
            ImGui.SameLine();
            
            labels.TryGetValue(positionKind, out var previewText);
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo($"##combo{label}", string.IsNullOrWhiteSpace(previewText) ? $"{positionKind}" : previewText)) {
                foreach (var value in Enum.GetValues<PositionKind>()) {
                    labels.TryGetValue(value, out var text);
                    if (ImGui.Selectable(string.IsNullOrWhiteSpace(text) ? $"{value}" : text)) {
                        positionKind = value;
                        anyChange = true;
                    }
                }
                
                ImGui.EndCombo();
            }
            
            ImGui.SameLine();
            
            ImGui.TextDisabled($"({offset / max * 100:F0}%%)");
            ImGui.SameLine();
            ImGui.Text(label);
        }
        
        
        PositionEditor("Horizontal", ref TweakConfig.AddonTalkConfig.Position.X, ref TweakConfig.AddonTalkConfig.HorizontalPositionKind, horizontalPositionKindLabels, Device.Instance()->Width);
        PositionEditor("Vertical", ref TweakConfig.AddonTalkConfig.Position.Y, ref TweakConfig.AddonTalkConfig.VerticalPositionKind, verticalPositionKindLabels, Device.Instance()->Height);
        
        if (anyChange) Enable();
    }
}
