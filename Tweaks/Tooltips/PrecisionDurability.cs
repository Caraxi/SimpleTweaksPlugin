﻿using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool ShouldSerializePrecisionDurabilityTrailingZeros() => false;
        public bool PrecisionDurabilityTrailingZeros = true;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    
    public class PrecisionDurability : TooltipTweaks.SubTweak {
        public override string Name => "Precise Durability";
        public override string Description => "Show partial percentages for durability.";

        public class Configs : TweakConfig {
            public bool TrailingZero = true;
        }
        
        public Configs Config { get; private set; }

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs() {TrailingZero = PluginConfig.TooltipTweaks.PrecisionDurabilityTrailingZeros};
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            base.Disable();
        }

        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {
            var c = tooltip[DurabilityPercent];
            if (c != null && !(c.Payloads[0] is TextPayload tp && tp.Text.StartsWith("?"))) {
                tooltip[DurabilityPercent] = new SeString(new List<Payload>() { new TextPayload((itemInfo.Condition / 300f).ToString(Config.TrailingZero ? "F2" : "0.##") + "%") });
            }

        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox($"Trailing Zeros###{GetType().Name}TrailingZeros", ref Config.TrailingZero);
        };
    }
}
