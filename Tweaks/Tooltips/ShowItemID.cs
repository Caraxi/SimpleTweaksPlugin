using Dalamud.Game.Chat.SeStringHandling.Payloads;
using ImGuiNET;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltip.TooltipField;
namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class ShowItemID : TooltipTweaks.SubTweak {
        public override string Name => "Show Item ID";
        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, TooltipTweaks.ItemInfo itemInfo) {
            var seStr = tooltip[ItemUiCategory];
            if (seStr == null) return;
            var id = Plugin.PluginInterface.Framework.Gui.HoveredItem;
            if (id < 2000000) id %= 500000;
            seStr.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, 3));
            seStr.Payloads.Add(new TextPayload($"   [{id}]"));
            seStr.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
            tooltip[ItemUiCategory] = seStr;
        }
    }
}