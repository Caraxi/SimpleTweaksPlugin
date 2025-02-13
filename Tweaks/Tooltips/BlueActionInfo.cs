using System;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Improved Blue Mage Action Tooltips")]
[TweakDescription("Adds Damage Type, Aspect and Rank to blue mage actions.")]
[Changelog("1.10.8.0", "Fixed tweak incorrectly applying to some pseudo-actions.")]
public unsafe class BlueActionInfo : TooltipTweaks.SubTweak {
    public override void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Action.Id == 0) return;
        var aozAction = Service.Data.Excel.GetSheet<AozAction>().FirstOrNull(a => a.Action.RowId == Action.Id);
        if (aozAction?.Action == null) return;
        var aozActionTransient = Service.Data.Excel.GetSheet<AozActionTransient>().GetRowOrDefault(aozAction.Value.RowId);
        if (aozActionTransient?.Stats == null) return;
        var descriptionString = GetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description);
        if (descriptionString.TextValue.Contains(Service.ClientState.ClientLanguage switch {
                Dalamud.Game.ClientLanguage.English => "Rank: ★",
                Dalamud.Game.ClientLanguage.German => "Rang: ★",
                Dalamud.Game.ClientLanguage.French => "Rang: ★",
                Dalamud.Game.ClientLanguage.Japanese => "ランク：★",
                _ => "Rank: ★" })) return; // Don't append when it already exists.
        var infoStr = aozActionTransient.Value.Stats.ExtractText();
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(new UIForegroundPayload(500));
        descriptionString.Append(new UIGlowPayload(7));
        descriptionString.Append(new TextPayload($"Blue Magic Spell #{aozActionTransient.Value.Number}"));
        descriptionString.Append(new UIForegroundPayload(0));
        descriptionString.Append(new UIGlowPayload(0));
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(infoStr);
        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, descriptionString);
        } catch (Exception ex) {
            SimpleLog.Error(ex);
            Plugin.Error(this, ex);
        }
    }
}

