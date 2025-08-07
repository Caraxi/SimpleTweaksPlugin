using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Tooltips;

[TweakName("Materia Stats")]
[TweakDescription("Includes an item's attached materia when displaying the stats.")]
[TweakAutoConfig]
public class MateriaStats : TooltipTweaks.SubTweak {
    public class Configs : TweakConfig {
        public bool Total = true;
        public bool Delta;
        public bool Colour;
        public bool SimpleCombined;
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.BeginGroup();
        if (ImGui.Checkbox(LocString("Show Total") + "##materiaStatsTooltipTweak", ref Config.Total)) {
            if (!Config.Total && !Config.Delta) {
                Config.Delta = true;
            }

            hasChanged = true;
        }

        if (ImGui.Checkbox(LocString("Show Delta") + "##materiaStatsTooltipTweak", ref Config.Delta)) {
            if (!Config.Total && !Config.Delta) {
                Config.Total = true;
            }

            hasChanged = true;
        }

        ImGui.EndGroup();

        if (Config.Total && Config.Delta) {
            var y = ImGui.GetCursorPosY();
            var groupSize = ImGui.GetItemRectSize();
            ImGui.SameLine();

            var text = LocString("Simplified Combined Display");
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (groupSize.Y / 2) - (textSize.Y / 2));
            hasChanged |= ImGui.Checkbox($"{text}##materiaStatSTooltipTweak", ref Config.SimpleCombined);
            ImGui.SetCursorPosY(y);
        }

        hasChanged |= ImGui.Checkbox(LocString("Colour Value") + "##materiaStatsTooltipTweak", ref Config.Colour);
    }

    public IEnumerable<TooltipTweaks.ItemTooltipField> Fields() {
        yield return TooltipTweaks.ItemTooltipField.Param0;
        yield return TooltipTweaks.ItemTooltipField.Param1;
        yield return TooltipTweaks.ItemTooltipField.Param2;
        yield return TooltipTweaks.ItemTooltipField.Param3;
        yield return TooltipTweaks.ItemTooltipField.Param4;
        yield return TooltipTweaks.ItemTooltipField.Param5;
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (!(Config.Delta || Config.Total == false)) Config.Total = true; // Config invalid check
        try {
            var item = Service.Data.Excel.GetSheet<Item>().GetRowOrDefault(Item.ItemId);
            if (item == null) return;
            if (item.Value.MateriaSlotCount == 0) return;
            var itemLevel = Service.Data.Excel.GetSheet<ExtendedItemLevel>().GetRowOrDefault(item.Value.LevelItem.RowId);
            if (itemLevel == null) return;
            var baseParams = new Dictionary<uint, ExtendedBaseParam>();
            var baseParamDeltas = new Dictionary<uint, int>();
            var baseParamOriginal = new Dictionary<uint, int>();
            var baseParamLimits = new Dictionary<uint, int>();

            for (var i = 0; i < item.Value.BaseParam.Count; i++) {
                var bp = item.Value.BaseParam[i].Value.GetExtension<ExtendedBaseParam, BaseParam>();
                var val = item.Value.BaseParamValue[i];
                if (val == 0 || bp.RowId == 0) continue;
                baseParams.Add(bp.RowId, bp);
                baseParamDeltas.Add(bp.RowId, 0);
                baseParamOriginal.Add(bp.RowId, val);
                baseParamLimits.Add(bp.RowId, (int)Math.Round(itemLevel.Value.BaseParam[(int)bp.RowId] * (bp.EquipSlotCategoryPct[(int)item.Value.EquipSlotCategory.RowId] / 1000f), MidpointRounding.AwayFromZero));
            }

            if (Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)) {
                for (var i = 0; i < item.Value.BaseParamSpecial.Count; i++) {
                    var bp = item.Value.BaseParamSpecial[i].Value.GetExtension<ExtendedBaseParam, BaseParam>();
                    var val = item.Value.BaseParamValueSpecial[i];
                    if (val == 0 || bp.RowId == 0) continue;
                    if (baseParamOriginal.ContainsKey(bp.BaseParam.RowId)) baseParamOriginal[bp.BaseParam.RowId] += val;
                }
            }

            if (baseParamDeltas.Count == 0) return;

            var pItem = Item;

            for (var i = 0; i < 5; i++) {
                var materiaId = pItem.Materia[i];

                var materia = Service.Data.Excel.GetSheet<Materia>().GetRowOrDefault(materiaId);
                if (materia == null) continue;
                var level = pItem.MateriaGrades[i];
                if (level > materia.Value.Value.Count) continue;

                if (materia.Value.BaseParam.RowId == 0) continue;
                if (materia.Value.BaseParam.ValueNullable == null) continue;
                if (!baseParamDeltas.ContainsKey(materia.Value.BaseParam.RowId)) {
                    var bp = Service.Data.Excel.GetSheet<ExtendedBaseParam>().GetRowOrDefault(materia.Value.BaseParam.RowId);
                    if (bp == null) continue;
                    baseParams.Add(materia.Value.BaseParam.RowId, bp.Value);
                    baseParamDeltas.Add(materia.Value.BaseParam.RowId, materia.Value.Value[level]);
                    baseParamOriginal.Add(materia.Value.BaseParam.RowId, 0);
                    baseParamLimits.Add(materia.Value.BaseParam.RowId, (int)Math.Round(itemLevel.Value.BaseParam[(int)materia.Value.BaseParam.RowId] * (bp.Value.EquipSlotCategoryPct[(int)item.Value.EquipSlotCategory.RowId] / 1000f), MidpointRounding.AwayFromZero));
                    continue;
                }

                baseParamDeltas[materia.Value.BaseParam.RowId] += materia.Value.Value[level];
            }

            foreach (var bp in baseParamDeltas) {
                
                var param = baseParams[bp.Key];
                if (bp.Value == 0) continue;
                var hasApplied = false;
                foreach (var field in Fields().Take(numberArrayData->IntArray[21])) {
                    var data = GetTooltipString(stringArrayData, field);
                    if (data == null) continue;
                    if (data.TextValue.Contains(param.BaseParam.Name.ExtractText())) {
                        hasApplied = true;
                        if (data.TextValue.EndsWith("]")) continue;
                        ApplyMateriaDifference(data, baseParamDeltas[param.RowId], baseParamOriginal[param.RowId], baseParamLimits[param.RowId]);
                        try {
                            SetTooltipString(stringArrayData, field, data);
                        } catch (Exception ex) {
                            Plugin.Error(this, ex);
                        }
                    }
                }

                if (!hasApplied) {
                    var baseParamLines = numberArrayData->IntArray[21];
                    if (baseParamLines < 8) {
                        var seString = new SeString();
                        seString.Payloads.Add(new TextPayload(param.BaseParam.Name.ExtractText()));
                        seString.Payloads.Add(new TextPayload($" +{baseParamOriginal[param.RowId]}"));
                        ApplyMateriaDifference(seString, baseParamDeltas[param.RowId], baseParamOriginal[param.RowId], baseParamLimits[param.RowId]);

                        try {
                            SetTooltipString(stringArrayData, (TooltipTweaks.ItemTooltipField)(37 + baseParamLines), seString);
                            numberArrayData->IntArray[21] += 1;
                        } catch (Exception ex) {
                            Plugin.Error(this, ex);
                        }
                    }
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void ApplyMateriaDifference(SeString data, int delta, int original, int limit) {
        data.Payloads.Add(new TextPayload($" ["));
        var totalValue = original + delta;
        var deltaValue = delta;
        var exceedLimit = false;
        if (totalValue > limit) {
            exceedLimit = true;
            totalValue = limit;
            deltaValue = limit - original;
        }

        if (Config.Delta) {
            if (!(Config.Total && Config.SimpleCombined)) data.Payloads.Add(new TextPayload($"+"));
            if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload((ushort)(exceedLimit ? 14 : 500)));
            data.Payloads.Add(new TextPayload($"{deltaValue}"));
            if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload(0));
            if (Config.Total && !Config.SimpleCombined) {
                data.Payloads.Add(new TextPayload("="));
            } else if (Config.Total) {
                data.Payloads.Add(new TextPayload(" "));
            }
        }

        if (Config.Total) {
            if (Config.Colour) data.Payloads.Add(new UIForegroundPayload((ushort)(exceedLimit ? 14 : 500)));
            data.Payloads.Add(new TextPayload($"{totalValue}"));
            if (Config.Colour) data.Payloads.Add(new UIForegroundPayload(0));
        }

        data.Payloads.Add(new TextPayload("]"));
    }
}
