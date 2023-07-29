using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public class PrecisionSpiritbond : SubTweak {
    public override string Name => "Precise Spiritbond";
    public override string Description => "Show partial percentages for Spiritbond.";

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
        var c = GetTooltipString(stringArrayData, SpiritbondPercent);
        if (c == null || c.TextValue.StartsWith("?")) return;
        try {
            SetTooltipString(stringArrayData, SpiritbondPercent, (Item.Spiritbond / 100f).ToString(Config.TrailingZero ? "F2" : "0.##") + "%");
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox(LocString("Trailing Zeros") + $"###{GetType().Name}TrailingZeros", ref Config.TrailingZero);
    };
}