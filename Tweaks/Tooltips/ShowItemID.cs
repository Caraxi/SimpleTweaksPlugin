using System;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static SimpleTweaksPlugin.Tweaks.TooltipTweaks.ItemTooltipField;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

[TweakName("Show ID")]
[TweakDescription("Show the ID of actions and items on their tooltips.")]
[TweakAutoConfig]
public class ShowItemID : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        [TweakConfigOption("Use Hexadecimal ID")]
        public bool Hex;

        public bool ShouldShowBoth() => Hex;
        [TweakConfigOption("Show Both HEX and Decimal", 1, ConditionalDisplay = true, SameLine = true)]
        public bool Both;
        
        [TweakConfigOption("Show Resolved Action ID", 2)]
        public bool ShowResolvedActionId = true;
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
        try {
            SetTooltipString(stringArrayData, ItemUiCategory, seStr);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        
    }

    public override unsafe void OnActionTooltip(AtkUnitBase* addon, TooltipTweaks.HoveredActionDetail action) {
        if (addon->UldManager.NodeList == null || addon->UldManager.NodeListCount < 29) return;
        var categoryText = (AtkTextNode*) addon->UldManager.NodeList[28];
        if (categoryText == null) return;
        var seStr = Common.ReadSeString(categoryText->NodeText.StringPtr);
        if (seStr.Payloads.Count <= 1) {
            if (seStr.Payloads.Count >= 1) {
                seStr.Payloads.Add(new TextPayload("   "));
            }
            seStr.Payloads.Add(new UIForegroundPayload(3));
            var id = Config.ShowResolvedActionId ? ActionManager.Instance()->GetAdjustedActionId(action.Id) : action.Id;
            seStr.Payloads.Add(new TextPayload($"["));
            if (Config.Hex == false || Config.Both) {
                seStr.Payloads.Add(new TextPayload($"{id}"));
            }
            if (Config.Hex) {
                if (Config.Both) seStr.Payloads.Add(new TextPayload(" - "));
                seStr.Payloads.Add(new TextPayload($"0x{id:X}"));
            }
            seStr.Payloads.Add(new TextPayload($"]"));
            seStr.Payloads.Add(new UIForegroundPayload(0));
            categoryText->SetText(seStr.Encode());
        }
            
    }
}