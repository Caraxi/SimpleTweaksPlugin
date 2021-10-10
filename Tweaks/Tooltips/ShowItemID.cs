using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.GameStructs;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class ShowItemID : TooltipTweaks.SubTweak {
        public override string Name => "Show ID";
        public override string Description => "Show the ID of actions and items on their tooltips.";

        public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
            var seStr = GetTooltipString(stringArrayData, ItemUiCategory);
            if (seStr == null) return;
            if (seStr.TextValue.EndsWith("]")) return;
            var id = Service.GameGui.HoveredItem;
            if (id < 2000000) id %= 500000;
            seStr.Payloads.Add(new UIForegroundPayload(3));
            seStr.Payloads.Add(new TextPayload($"   [{id}]"));
            seStr.Payloads.Add(new UIForegroundPayload(0));
            stringArrayData->SetValue((int) ItemUiCategory, seStr.Encode(), false);
        }

        public override unsafe void OnActionTooltip(AddonActionDetail* addon, TooltipTweaks.HoveredAction action) {
            if (addon->AtkUnitBase.UldManager.NodeList == null || addon->AtkUnitBase.UldManager.NodeListCount < 29) return;
            var categoryText = (AtkTextNode*) addon->AtkUnitBase.UldManager.NodeList[28];
            if (categoryText == null) return;
            var seStr = Plugin.Common.ReadSeString(categoryText->NodeText.StringPtr);
            if (seStr.Payloads.Count <= 1) {
                if (seStr.Payloads.Count >= 1) {
                    seStr.Payloads.Add(new TextPayload("   "));
                }
                seStr.Payloads.Add(new UIForegroundPayload(3));
                seStr.Payloads.Add(new TextPayload($"[{action.Id}]"));
                seStr.Payloads.Add(new UIForegroundPayload(0));
                categoryText->SetText(seStr.Encode());
            }
            
        }
    }
}
