using System.Collections.Generic;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool PrecisionSpiritbondTrailingZeros = true;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public class PrecisionSpiritbond : SubTweak {
        public override string Name => "Precise Spiritbond";
        public override string Description => "Show partial percentages for Spiritbond.";

        public override void OnItemTooltip(ItemTooltip tooltip, InventoryItem itemInfo) {
            var c = tooltip[SpiritbondPercent];
            if (c != null && !(c.Payloads[0] is TextPayload tp && tp.Text.StartsWith("?"))) {
                tooltip[SpiritbondPercent] = new SeString(new List<Payload>() { new TextPayload((itemInfo.Spiritbond / 100f).ToString(PluginConfig.TooltipTweaks.PrecisionSpiritbondTrailingZeros ? "F2" : "0.##") + "%") });
            }
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox($"Trailing Zeros###{GetType().Name}TrailingZeros", ref PluginConfig.TooltipTweaks.PrecisionSpiritbondTrailingZeros);
        };
    }

}

