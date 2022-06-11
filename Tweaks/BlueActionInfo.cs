using System.Linq;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class BlueActionInfo : TooltipTweaks.SubTweak {
    public override string Name => "Improved Blue Mage Action Tooltips";
    public override string Description => "Adds Damage Type, Aspect and Rank to blue mage actions.";
    public override void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var aozAction = Service.Data.Excel.GetSheet<AozAction>()?.FirstOrDefault(a => a.Action.Row == Action.Id);
        if (aozAction?.Action?.Value == null) return;
        var aozActionTransient = Service.Data.Excel.GetSheet<AozActionTransient>()?.GetRow(aozAction.RowId);
        if (aozActionTransient?.Stats == null) return;
        var descriptionString = GetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description);
        if (descriptionString.TextValue.Contains("Rank: ★")) return; // Don't append when it already exists.
        var infoStr = aozActionTransient.Stats.ToDalamudString();
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(NewLinePayload.Payload);
        descriptionString.Append(infoStr);
        SetTooltipString(stringArrayData, TooltipTweaks.ActionTooltipField.Description, descriptionString);
    }
}

