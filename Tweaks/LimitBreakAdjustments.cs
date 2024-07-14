using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Limit Break Adjustments")]
[TweakDescription("Simple customization of the limit break bars.")]
[TweakReleaseVersion("1.8.2.0")]
public unsafe class LimitBreakAdjustments : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public static readonly Configs Default = new();

        public float BarRotation;
        public Vector2 BarSpacing = new(164, 0);
        public Vector2 TextPosition = new(12, 1);
        public bool HideText;
    }

    public Configs Config { get; private set; }
    
    protected void DrawConfig(ref bool _) {
        ImGui.DragFloat("Bar Rotation", ref Config.BarRotation, 0.5f, -360, 720, "%.1f");
        while (Config.BarRotation >= 360) Config.BarRotation -= 360;
        while (Config.BarRotation < 0) Config.BarRotation += 360;
        
        ImGui.DragFloat2("Bar Seperation", ref Config.BarSpacing);
        
        ImGui.Checkbox("Hide Text", ref Config.HideText);
        ImGui.DragFloat2("Text Position", ref Config.TextPosition);
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Common.FrameworkUpdate += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate() {
        Update(Config);
    }

    private void Update(Configs config) {
        var lbAddon = Common.GetUnitBase("_LimitBreak");
        if (lbAddon == null) return;

        var containerNode = lbAddon->GetNodeById(3);
        if (containerNode == null) return;

        var textNode = lbAddon->GetTextNodeById(2);
        if (textNode == null) return;
        
        textNode->AtkResNode.ToggleVisibility(!config.HideText);
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
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        Update(Configs.Default);
        SaveConfig(Config);
    }
}
