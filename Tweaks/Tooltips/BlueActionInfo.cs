﻿using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Improved Blue Mage Action Tooltips")]
[TweakDescription("Adds Damage Type, Aspect and Rank to blue mage actions.")]
public unsafe class BlueActionInfo : TooltipTweaks.SubTweak {
    public override void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var aozAction = Service.Data.Excel.GetSheet<AozAction>()?.FirstOrDefault(a => a.Action.Row == Action.Id);
        if (aozAction?.Action?.Value == null) return;
        var aozActionTransient = Service.Data.Excel.GetSheet<AozActionTransient>()?.GetRow(aozAction.RowId);
        if (aozActionTransient?.Stats == null) return;
        var descriptionString = GetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description);
        if (descriptionString.TextValue.Contains(Service.ClientState.ClientLanguage switch {
                Dalamud.Game.ClientLanguage.English => "Rank: ★",
                Dalamud.Game.ClientLanguage.German => "Rang: ★",
                Dalamud.Game.ClientLanguage.French => "Rang: ★",
                Dalamud.Game.ClientLanguage.Japanese => "ランク：★",
                _ => "Rank: ★" })) return; // Don't append when it already exists.
        var infoStr = aozActionTransient.Stats.ToDalamudString();
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(new UIForegroundPayload(500));
        descriptionString.Append(new UIGlowPayload(7));
        descriptionString.Append(new TextPayload($"Blue Magic Spell #{aozActionTransient.Number}"));
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

