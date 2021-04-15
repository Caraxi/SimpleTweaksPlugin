
using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool DesynthesisDelta = false;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class DesynthesisSkill : TooltipTweaks.SubTweak {
        public override string Name => "Show Desynthesis Skill";
        public override string Description => "Shows your current desynthesis level when viewing a desynthesizable item.";

        private readonly uint[] desynthesisInDescription = { 46, 56, 65, 66, 67, 68, 69, 70, 71, 72 };

        public override unsafe void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {

            var id = PluginInterface.Framework.Gui.HoveredItem;
            if (id < 2000000) {
                id %= 500000;

                var item = PluginInterface.Data.Excel.GetSheet<Sheets.ExtendedItem>().GetRow((uint)id);
                if (item != null && item.Desynth > 0) {
                    var classJobOffset = 2 * (int)(item.ClassJobRepair.Row - 8);
                    var desynthLevel = *(ushort*)(Common.PlayerStaticAddress + (0x69A + classJobOffset)) / 100f;
                    var desynthDelta = item.LevelItem.Row - desynthLevel;

                    var useDescription = desynthesisInDescription.Contains(item.ItemSearchCategory.Row);

                    var seStr = tooltip[useDescription ? ItemDescription : ExtractableProjectableDesynthesizable];

                    if (seStr != null) {
                        if (seStr.Payloads.Last() is TextPayload textPayload) {
                            if (PluginConfig.TooltipTweaks.DesynthesisDelta) {
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#}");
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthDelta:+#;-#})");
                            } else {
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                            }
                            tooltip[useDescription ? ItemDescription : ExtractableProjectableDesynthesizable] = seStr;
                        }
                    }
                }
            }
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox($"Desynthesis Delta###{GetType().Name}DesynthesisDelta", ref PluginConfig.TooltipTweaks.DesynthesisDelta);
        };
    }
}
