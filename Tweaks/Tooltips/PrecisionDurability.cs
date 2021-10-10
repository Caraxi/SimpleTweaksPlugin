using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    
    public class PrecisionDurability : TooltipTweaks.SubTweak {
        public override string Name => "Precise Durability";
        public override string Description => "Show partial percentages for durability.";

        public class Configs : TweakConfig {
            public bool TrailingZero = true;
        }
        
        public Configs Config { get; private set; }

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            base.Disable();
        }

        public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
            var c = GetTooltipString(stringArrayData, DurabilityPercent);
            if (c == null || c.TextValue.StartsWith("?")) return;
            stringArrayData->SetValue((int)DurabilityPercent, (Item.Condition / 300f).ToString(Config.TrailingZero ? "F2" : "0.##") + "%", false);
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox($"Trailing Zeros###{GetType().Name}TrailingZeros", ref Config.TrailingZero);
        };
    }
}
