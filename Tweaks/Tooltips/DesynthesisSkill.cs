
using System;
using System.Linq;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class DesynthesisSkill : TooltipTweaks.SubTweak {
        public override string Name => "Show Desynthesis Skill";

        private IntPtr playerStaticAddress;
        private readonly uint[] desynthesisInDescription = { 46, 56, 65, 66, 67, 68, 69, 70, 71, 72 };

        public override void Setup() {
            playerStaticAddress = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
            base.Setup();
        }

        public override unsafe void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, TooltipTweaks.ItemInfo itemInfo) {

            var id = PluginInterface.Framework.Gui.HoveredItem;
            if (id < 2000000) {
                id %= 500000;

                var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint)id);
                if (item != null && item.Desynth > 0) {
                    var classJobOffset = 2 * (int)(item.ClassJobRepair.Row - 8);
                    var desynthLevel = *(ushort*)(playerStaticAddress + (0x69A + classJobOffset)) / 100f;

                    var useDescription = desynthesisInDescription.Contains(item.ItemSearchCategory.Row);

                    var seStr = tooltip[useDescription ? ItemDescription : ExtractableProjectableDesynthesizable];

                    if (seStr != null) {
                        if (seStr.Payloads.Last() is TextPayload textPayload) {
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                            textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                            tooltip[useDescription ? ItemDescription : ExtractableProjectableDesynthesizable] = seStr;
                        }
                    }
                }
            }


        }
    }
}
