using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Precise Durability")]
[TweakDescription("Show partial percentages for durability.")]
public class PrecisionDurability : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        public bool TrailingZero = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var c = GetTooltipString(stringArrayData, DurabilityPercent);
        if (c == null || c.TextValue.StartsWith("?")) return;
        try {
            SetTooltipString(stringArrayData, DurabilityPercent, (Item.Condition / 300f).ToString(Config.TrailingZero ? "F2" : "0.##") + "%");
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox(LocString("Trailing Zeros") + $"###{GetType().Name}TrailingZeros", ref Config.TrailingZero);
    }
}
