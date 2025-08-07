using System;
using System.Numerics;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Limit Break Adjustments")]
[TweakDescription("Simple customization of the limit break bars.")]
[TweakReleaseVersion("1.8.2.0")]
[TweakAutoConfig]
[Changelog("1.10.9.0", "Added preview for when not in a party")]
[Changelog("1.10.9.1", "Fixed limit break text appearing when it isn't supposed to")]
public unsafe class LimitBreakAdjustments : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public static readonly Configs Default = new();
        public float BarRotation;
        public Vector2 BarSpacing = new(164, 0);
        public Vector2 TextPosition = new(12, 1);
        public bool HideText;
    }

    [TweakConfig] public Configs Config { get; private set; }
    private bool previewLimitBreak;
    private byte previewVisibleCounter;
    private byte resetValue;

    protected void DrawConfig() {
        ImGui.DragFloat("Bar Rotation", ref Config.BarRotation, 0.5f, -360, 720, "%.1f");
        while (Config.BarRotation >= 360) Config.BarRotation -= 360;
        while (Config.BarRotation < 0) Config.BarRotation += 360;

        ImGui.DragFloat2("Bar Seperation", ref Config.BarSpacing);

        ImGui.Checkbox("Hide Text", ref Config.HideText);
        ImGui.DragFloat2("Text Position", ref Config.TextPosition);
        ImGui.Checkbox("Preview Bar", ref previewLimitBreak);

        if (previewLimitBreak) {
            if (previewVisibleCounter == 0) resetValue = LimitBreakController.Instance()->BarCount;
            LimitBreakController.Instance()->BarCount = 3;
            previewVisibleCounter = 2;
        }
    }

    private delegate void* SetValuesDelegate(LimitBreakController* controller, byte barCount, ushort currentUnits, ushort barUnits, byte a5, byte isPvp);

    [TweakHook, Signature("0F B6 44 24 ?? 88 41 0E", DetourName = nameof(SetValuesDetour))]
    private HookWrapper<SetValuesDelegate> setValuesHook;

    private void* SetValuesDetour(LimitBreakController* controller, byte barCount, ushort currentUnits, ushort barUnits, byte a5, byte isPvp) {
        previewLimitBreak = false;
        previewVisibleCounter = 0;
        return setValuesHook.Original(controller, barCount, currentUnits, barUnits, a5, isPvp);
    }


    [FrameworkUpdate]
    private void OnFrameworkUpdate() {
        if (previewVisibleCounter > 0) {
            previewVisibleCounter--;
            if (previewVisibleCounter == 0) {
                previewLimitBreak = false;
                LimitBreakController.Instance()->BarCount = resetValue;
            }
        }
        
        Update(Config);
    }

    private void Update(Configs config) {
        var lbAddon = Common.GetUnitBase("_LimitBreak");
        if (lbAddon == null) return;

        var containerNode = lbAddon->GetNodeById(3);
        if (containerNode == null) return;

        var textNode = lbAddon->GetTextNodeById(2);
        if (textNode == null) return;

        textNode->AtkResNode.ToggleVisibility(!config.HideText && LimitBreakController.Instance()->BarCount > 0);
        textNode->AtkResNode.SetPositionFloat(config.TextPosition.X, config.TextPosition.Y);

        var bars = new[] {
            (AtkComponentNode*) lbAddon->GetNodeById(6),
            (AtkComponentNode*) lbAddon->GetNodeById(5),
            (AtkComponentNode*) lbAddon->GetNodeById(4),
        };

        var p = new Vector2();

        foreach (var b in bars) {
            if (b == null) continue;
            b->AtkResNode.Rotation = config.BarRotation * MathF.PI / 180f;
            b->AtkResNode.SetPositionFloat(p.X, p.Y);
            b->AtkResNode.SetScale(1, 1);
            b->AtkResNode.DrawFlags |= 1;
            p += config.BarSpacing;
        }
    }

    protected override void Disable() {
        Update(Configs.Default);
        previewLimitBreak = false;
        LimitBreakController.Instance()->BarCount = 0;
        if (previewVisibleCounter > 0) {
            LimitBreakController.Instance()->BarCount = resetValue;
            previewVisibleCounter = 0;
        }
    }
}
