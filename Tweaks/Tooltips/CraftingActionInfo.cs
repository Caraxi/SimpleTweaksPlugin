using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Enums;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

public unsafe class CraftingActionInfo : TooltipTweaks.SubTweak {
    public override string Name => "Improved Crafting Action Tooltips";
    public override string Description => "Adds calculated efficiency of crafting actions to tooltips.";

    private DalamudLinkPayload? identifier;
    private string progressString;
    private string qualityString;
    
    public override void Enable() {
        progressString ??= Service.Data.Excel.GetSheet<Addon>()?.GetRow(213)?.Text?.RawString ?? "Progress";
        qualityString ??= Service.Data.Excel.GetSheet<Addon>()?.GetRow(216)?.Text?.RawString ?? "Quality";
        
        
        identifier = PluginInterface.AddChatLinkHandler((uint) LinkHandlerId.CraftingActionInfoIdentifier, (_, _) => { });
        base.Enable();
    }
    
    public override void Disable() {
        PluginInterface.RemoveChatLinkHandler((uint) LinkHandlerId.CraftingActionInfoIdentifier);
        base.Dispose();
    }
    
    public override void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (identifier == null) return;

        var agent = AgentCraftActionSimulator.Instance();
        if (agent == null) return;

        var progress = 0U;
        var quality = 0U;
        
        // Find Progress
        var p = (ProgressEfficiencyCalculation*) agent->Progress;
        for (var i = 0; i < sizeof(ProgressEfficiencyCalculations) / sizeof(ProgressEfficiencyCalculation); i++) {
            if (p == null) break;
            if (p->ActionId == Action.Id) {
                progress = p->ProgressIncrease;
                break;
            }
            p++;
        }
        
        var q = (QualityEfficiencyCalculation*) agent->Quality;
        for (var i = 0; i < sizeof(QualityEfficiencyCalculations) / sizeof(QualityEfficiencyCalculation); i++) {
            if (q == null) break;
            if (q->ActionId == Action.Id) {
                quality = q->QualityIncrease;
                break;
            }
            q++;
        }

        if (progress == 0 && quality == 0) return;
        
        var descriptionString = GetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description);
        if (descriptionString.Payloads.Any(payload => payload is DalamudLinkPayload { CommandId: (uint)LinkHandlerId.CraftingActionInfoIdentifier })) return; // Don't append when it already exists.
        
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(identifier);
        descriptionString.Append(RawPayload.LinkTerminator);
        descriptionString.Append(NewLinePayload.Payload);

        if (progress > 0) {
            descriptionString.Append(new UIForegroundPayload(500));
            descriptionString.Append(new UIGlowPayload(7));
            descriptionString.Append(new TextPayload($"{progressString}: "));
            descriptionString.Append(new UIForegroundPayload(0));
            descriptionString.Append(new UIGlowPayload(0));
            descriptionString.Append($"{progress}");
            if (quality > 0) descriptionString.Append(NewLinePayload.Payload);
        }

        if (quality > 0) {
            descriptionString.Append(new UIForegroundPayload(500));
            descriptionString.Append(new UIGlowPayload(7));
            descriptionString.Append(new TextPayload($"{qualityString}: "));
            descriptionString.Append(new UIForegroundPayload(0));
            descriptionString.Append(new UIGlowPayload(0));
            descriptionString.Append($"{quality}");
        }
        
        SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, descriptionString);
    }
}

