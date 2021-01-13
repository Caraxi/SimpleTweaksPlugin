using System.Collections.Generic;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool PrecisionDurabilityTrailingZeros = true;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    
    public class PrecisionDurability : TooltipTweaks.SubTweak {
        public override string Name => "Precise Durability";

        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {
            var c = tooltip[DurabilityPercent];
            if (c != null && !(c.Payloads[0] is TextPayload tp && tp.Text.StartsWith("?"))) {
                tooltip[DurabilityPercent] = new SeString(new List<Payload>() { new TextPayload((itemInfo.Condition / 300f).ToString(PluginConfig.TooltipTweaks.PrecisionDurabilityTrailingZeros ? "F2" : "0.##") + "%") });
            }

        }

        public override void DrawConfig(ref bool hasChanged) {
            base.DrawConfig(ref hasChanged);
            if (!Enabled) return;
            ImGui.SameLine();
            hasChanged |= ImGui.Checkbox($"Trailing Zeros###{GetType().Name}TrailingZeros", ref PluginConfig.TooltipTweaks.PrecisionDurabilityTrailingZeros);
        }
    }
}
