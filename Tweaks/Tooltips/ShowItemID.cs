using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public class ShowItemID : TooltipTweaks.SubTweak {
    public override string Name => "Show ID";
    public override string Description => "Show the ID of actions and items on their tooltips.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Use Hexadecimal ID")]
        public bool Hex = false;

        public bool ShouldShowBoth() => Hex;
        [TweakConfigOption("Show Both HEX and Decimal", 1, ConditionalDisplay = true, SameLine = true)]
        public bool Both = false;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var seStr = GetTooltipString(stringArrayData, ItemUiCategory);
        if (seStr == null) return;
        if (seStr.TextValue.EndsWith("]")) return;
        var id = Service.GameGui.HoveredItem;
        if (id < 2000000) id %= 500000;
        seStr.Payloads.Add(new UIForegroundPayload(3));
        seStr.Payloads.Add(new TextPayload($"   ["));
        if (Config.Hex == false || Config.Both) {
            seStr.Payloads.Add(new TextPayload($"{id}"));
        }
        if (Config.Hex) {
            if (Config.Both) seStr.Payloads.Add(new TextPayload(" - "));
            seStr.Payloads.Add(new TextPayload($"0x{id:X}"));
        }
        seStr.Payloads.Add(new TextPayload($"]"));
        seStr.Payloads.Add(new UIForegroundPayload(0));
        SetTooltipString(stringArrayData, ItemUiCategory, seStr);
    }

    public override unsafe void OnActionTooltip(AddonActionDetail* addon, TooltipTweaks.HoveredActionDetail action) {
        if (addon->AtkUnitBase.UldManager.NodeList == null || addon->AtkUnitBase.UldManager.NodeListCount < 29) return;
        var categoryText = (AtkTextNode*) addon->AtkUnitBase.UldManager.NodeList[28];
        if (categoryText == null) return;
        var seStr = Common.ReadSeString(categoryText->NodeText.StringPtr);
        if (seStr.Payloads.Count <= 1) {
            if (seStr.Payloads.Count >= 1) {
                seStr.Payloads.Add(new TextPayload("   "));
            }
            seStr.Payloads.Add(new UIForegroundPayload(3));
            seStr.Payloads.Add(new TextPayload($"["));
            if (Config.Hex == false || Config.Both) {
                seStr.Payloads.Add(new TextPayload($"{action.Id}"));
            }
            if (Config.Hex) {
                if (Config.Both) seStr.Payloads.Add(new TextPayload(" - "));
                seStr.Payloads.Add(new TextPayload($"0x{action.Id:X}"));
            }
            seStr.Payloads.Add(new TextPayload($"]"));
            seStr.Payloads.Add(new UIForegroundPayload(0));
            categoryText->SetText(seStr.Encode());
        }
            
    }
}