using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Show expected food and potion stats")]
[TweakDescription("Calculates the stat results a consumable will have based on your current stats.")]
[TweakAutoConfig]
public unsafe class FoodStats : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        public bool Highlight;
    }

    [TweakConfig] public Configs Config { get; private set; }

    private ExcelSheet<Item> itemSheet;
    private ExcelSheet<ItemFood> foodSheet;

    private readonly TextPayload potionPercentPayload = new("??%");
    private readonly TextPayload potionMaxPayload = new("????");
    private readonly TextPayload potionActualValuePayload = new("????");

    private SeString hpPotionEffectString;
    private SeString hpPotionCappedEffectString;

    protected override void Enable() {
        itemSheet = Service.Data.Excel.GetSheet<Item>();
        foodSheet = Service.Data.Excel.GetSheet<ItemFood>();
        Service.Data.Excel.GetSheet<BaseParam>();
        BuildPotionEffectStrings();
    }

    private void BuildPotionEffectStrings() {
        hpPotionEffectString = new SeString();
        hpPotionCappedEffectString = new SeString();

        var hpEffectString = Service.Data.Excel.GetSheet<Addon>().GetRow(998).Text.ToDalamudString();

        var removePercent = false;

        foreach (var p in hpEffectString.Payloads) {
            if (p is RawPayload rp) {
                switch (rp.Data[^2]) {
                    case 2:
                        if (Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(500));
                        hpPotionEffectString.Payloads.Add(potionPercentPayload);
                        if (Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(0));

                        hpPotionEffectString.Payloads.Add(new TextPayload(" ("));
                        if (Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(500));
                        hpPotionEffectString.Payloads.Add(potionActualValuePayload);
                        if (Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(0));
                        hpPotionEffectString.Payloads.Add(new TextPayload(")"));

                        hpPotionCappedEffectString.Payloads.Add(potionPercentPayload);
                        removePercent = true;
                        break;
                    case 3:
                        hpPotionEffectString.Payloads.Add(potionMaxPayload);

                        if (Config.Highlight) hpPotionCappedEffectString.Payloads.Add(new UIForegroundPayload(500));
                        hpPotionCappedEffectString.Payloads.Add(potionMaxPayload);
                        if (Config.Highlight) hpPotionCappedEffectString.Payloads.Add(new UIForegroundPayload(0));

                        break;
                    default:
                        hpPotionEffectString.Payloads.Add(p);
                        break;
                }
            } else {
                if (removePercent) {
                    removePercent = false;
                    if (p is TextPayload { Text: not null} tp && tp.Text.StartsWith('%')) {
                        hpPotionEffectString.Payloads.Add(new TextPayload(tp.Text[1..]));
                        hpPotionCappedEffectString.Payloads.Add(new TextPayload(tp.Text[1..]));
                    }

                    continue;
                }

                hpPotionEffectString.Payloads.Add(p);
                hpPotionCappedEffectString.Payloads.Add(p);
            }
        }

        foreach (var p in hpPotionEffectString.Payloads) {
            SimpleLog.Log($"Payload: {p}");
        }
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox(LocString("Highlight Active"), ref Config.Highlight);
        if (hasChanged) BuildPotionEffectStrings();
    }

    public override void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (Service.ClientState.LocalPlayer == null) return;
        var id = AgentItemDetail.Instance()->ItemId;
        if (id >= 2000000) return;
        var hq = id >= 500000;
        id %= 500000;
        if (!itemSheet.TryGetRow((uint)id, out var item)) return;
        if (item.ItemAction.RowId == 0) return;
        
        var action = item.ItemAction.Value;
        

        if (action is { Type : 847 }) {
            // Healing Potion
            var percentNumber = hq ? action.DataHQ[0] : action.Data[0];
            var percent = percentNumber / 100f;

            var max = hq ? action.DataHQ[1] : action.Data[1];
            var actual = Math.Floor(Service.ClientState.LocalPlayer.MaxHp * percent);

            var seStr = hpPotionEffectString;

            if (actual > max) {
                actual = max;
                if (Config.Highlight) seStr = hpPotionCappedEffectString;
            }

            potionPercentPayload.Text = $"{percentNumber}%";
            potionMaxPayload.Text = $"{max}";
            potionActualValuePayload.Text = $"{actual}";

            SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.Effects, seStr);
        }

        if (action is not { Type: 844 or 845 or 846 }) return;

        if (!foodSheet.TryGetRow(hq ? action.DataHQ[1] : action.Data[1], out var itemFood)) return;
        var payloads = new List<Payload>();
        var hasChange = false;

        foreach (var bonus in itemFood.Params) {
            if (bonus.BaseParam.RowId == 0) continue;
            var param = bonus.BaseParam;
            var value = hq ? bonus.ValueHQ : bonus.Value;
            var max = hq ? bonus.MaxHQ : bonus.Max;
            if (bonus.IsRelative) {
                hasChange = true;

                var currentStat = PlayerState.Instance()->Attributes[(int)bonus.BaseParam.RowId];
                var relativeAdd = (short)(currentStat * (value / 100f));
                var change = relativeAdd > max ? max : relativeAdd;

                if (payloads.Count > 0) payloads.Add(new TextPayload("\n"));

                payloads.Add(new TextPayload($"{param.Value.Name} +"));

                if (Config.Highlight && change < max) payloads.Add(new UIForegroundPayload(500));
                payloads.Add(new TextPayload($"{value}%"));
                if (change < max) {
                    if (Config.Highlight) payloads.Add(new UIForegroundPayload(0));
                    payloads.Add(new TextPayload($" (Current "));
                    if (Config.Highlight) payloads.Add(new UIForegroundPayload(500));
                    payloads.Add(new TextPayload($"{change}"));
                    if (Config.Highlight) payloads.Add(new UIForegroundPayload(0));
                    payloads.Add(new TextPayload($")"));
                }

                payloads.Add(new TextPayload(" (Max "));
                if (Config.Highlight && change == max) payloads.Add(new UIForegroundPayload(500));
                payloads.Add(new TextPayload($"{max}"));
                if (Config.Highlight && change == max) payloads.Add(new UIForegroundPayload(0));
                payloads.Add(new TextPayload(")"));
            } else {
                if (payloads.Count > 0) payloads.Add(new TextPayload("\n"));
                payloads.Add(new TextPayload($"{param.Value.Name} +{value}"));
            }
        }

        if (payloads.Count <= 0 || !hasChange) return;
        var seStr2 = new SeString(payloads);
        try {
            SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.Effects, seStr2);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }
}
