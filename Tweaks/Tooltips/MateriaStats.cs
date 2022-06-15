﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.Tooltips; 

public class MateriaStats : TooltipTweaks.SubTweak {
    public override string Name => "Materia Stats";
    public override string Description => "Includes an item's attached materia when displaying the stats.";
        
    public class Configs : TweakConfig {
        public bool Total = true;
        public bool Delta;
        public bool Colour;
        public bool SimpleCombined;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
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
    };

    public IEnumerable<TooltipTweaks.ItemTooltipField> Fields() {
        yield return TooltipTweaks.ItemTooltipField.Param0;
        yield return TooltipTweaks.ItemTooltipField.Param1;
        yield return TooltipTweaks.ItemTooltipField.Param2;
        yield return TooltipTweaks.ItemTooltipField.Param3;
        yield return TooltipTweaks.ItemTooltipField.Param4;
        yield return TooltipTweaks.ItemTooltipField.Param5;
    }

    private ExcelSheet<ExtendedItem> itemSheet;
    private ExcelSheet<ExtendedItemLevel> itemLevelSheet;
    private ExcelSheet<ExtendedBaseParam> bpSheet;
    private ExcelSheet<Materia> materiaSheet;

    public override void Enable() {
        itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
        itemLevelSheet = Service.Data.Excel.GetSheet<ExtendedItemLevel>();
        bpSheet = Service.Data.Excel.GetSheet<ExtendedBaseParam>();
        materiaSheet = Service.Data.Excel.GetSheet<Materia>();
        if (itemSheet == null || itemLevelSheet == null || bpSheet == null || materiaSheet == null) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    public override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }


    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        if (!(Config.Delta || Config.Total == false)) Config.Total = true; // Config invalid check
        try {
            var item = itemSheet.GetRow(Item.ItemID);
            if (item == null) return;
            if (item.MateriaSlotCount == 0) return;
            var itemLevel = itemLevelSheet.GetRow(item.LevelItem.Row);
            if (itemLevel == null) return;
            var baseParams = new Dictionary<uint, ExtendedBaseParam>();
            var baseParamDeltas = new Dictionary<uint, int>();
            var baseParamOriginal = new Dictionary<uint, int>();
            var baseParamLimits = new Dictionary<uint, int>();
            foreach (var bp in item.BaseParam) {
                if (bp.Value == 0 || bp.BaseParam.Row == 0) continue;
                baseParamDeltas.Add(bp.BaseParam.Row, 0);
                baseParamOriginal.Add(bp.BaseParam.Row, bp.Value);
                if (bp.BaseParam.Value != null) {
                    baseParamLimits.Add(bp.BaseParam.Row, (int)Math.Ceiling(itemLevel.BaseParam[bp.BaseParam.Row] * (bp.BaseParam.Value.EquipSlotCategoryPct[item.EquipSlotCategory.Row] / 100f)));
                    baseParams.Add(bp.BaseParam.Row, bp.BaseParam.Value);
                }
            }

            if (Item.Flags.HasFlag(InventoryItem.ItemFlags.HQ)) {
                foreach (var bp in item.BaseParamSpecial) {
                    if (bp.Value == 0 || bp.BaseParam.Row == 0) continue;
                    if (baseParamOriginal.ContainsKey(bp.BaseParam.Row)) baseParamOriginal[bp.BaseParam.Row] += bp.Value;
                }
            }

            if (baseParamDeltas.Count == 0) return;


            var pItem = Item;
            var materiaId = pItem.Materia;
            var level = pItem.MateriaGrade;

            for (var i = 0; i < 5; i++, materiaId++, level++) {
                if (*level >= 10) continue;
                var materia = materiaSheet.GetRow(*materiaId);
                if (materia == null) continue;
                if (materia.BaseParam.Row == 0) continue;
                if (materia.BaseParam.Value == null) continue;
                if (!baseParamDeltas.ContainsKey(materia.BaseParam.Row)) {
                    var bp = Service.Data.Excel.GetSheet<ExtendedBaseParam>()?.GetRow(materia.BaseParam.Row);
                    if (bp == null) continue;
                    baseParams.Add(materia.BaseParam.Row, bp);
                    baseParamDeltas.Add(materia.BaseParam.Row, materia.Value[*level]);
                    baseParamOriginal.Add(materia.BaseParam.Row, 0);
                    baseParamLimits.Add(materia.BaseParam.Row, (int) Math.Ceiling(itemLevel.BaseParam[materia.BaseParam.Row] * (bp.EquipSlotCategoryPct[item.EquipSlotCategory.Row] / 100f)));
                    continue;
                }
                baseParamDeltas[materia.BaseParam.Row] += materia.Value[*level];
            }
            
            foreach (var bp in baseParamDeltas) {
                var param = baseParams[bp.Key];
                if (bp.Value == 0) continue;
                var hasApplied = false;
                foreach (var field in Fields().Take(numberArrayData->IntArray[21])) {
                    var data = GetTooltipString(stringArrayData, field);
                    if (data == null) continue;
                    if (data.TextValue.Contains(param.Name)) {
                        hasApplied = true;
                        if (data.TextValue.EndsWith("]")) continue;
                        ApplyMateriaDifference(data, baseParamDeltas[param.RowId], baseParamOriginal[param.RowId], baseParamLimits[param.RowId]);
                        SetTooltipString(stringArrayData, field, data);
                    }

                }

                if (!hasApplied) {
                    var baseParamLines = numberArrayData->IntArray[21];
                    if (baseParamLines < 8) {
                        var seString = new SeString();
                        seString.Payloads.Add(new TextPayload(param.Name));
                        seString.Payloads.Add(new TextPayload($" +{baseParamOriginal[param.RowId]}"));
                        ApplyMateriaDifference(seString, baseParamDeltas[param.RowId], baseParamOriginal[param.RowId], baseParamLimits[param.RowId]);

                        SetTooltipString(stringArrayData, (TooltipTweaks.ItemTooltipField) (37 + baseParamLines), seString);
                        numberArrayData->IntArray[21] += 1;
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
            if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload((ushort) (exceedLimit ? 14 : 500)));
            data.Payloads.Add(new TextPayload($"{deltaValue}"));
            if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload(0));
            if (Config.Total && !Config.SimpleCombined) {
                data.Payloads.Add(new TextPayload("="));
            } else if (Config.Total) {
                data.Payloads.Add(new TextPayload(" "));
            }
        }
        if (Config.Total) {
            if (Config.Colour) data.Payloads.Add(new UIForegroundPayload((ushort) (exceedLimit ? 14 : 500)));
            data.Payloads.Add(new TextPayload($"{totalValue}"));
            if (Config.Colour) data.Payloads.Add(new UIForegroundPayload(0));
        }

        data.Payloads.Add(new TextPayload("]"));
    }

}