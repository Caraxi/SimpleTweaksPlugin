﻿using System;
using System.Collections.Generic;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.Tweaks.Tooltips;
using BaseParam = SimpleTweaksPlugin.Sheets.BaseParam;
using ItemLevel = SimpleTweaksPlugin.Sheets.ItemLevel;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public MateriaStats.Configs MateriaStats = new MateriaStats.Configs();
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    
    public class MateriaStats : TooltipTweaks.SubTweak {
        public override string Name => "Materia Stats";
        
        public class Configs {
            public bool Total = true;
            public bool Delta;
            public bool Colour;
            public bool SimpleCombined;
        }

        public Configs Config => PluginConfig.TooltipTweaks.MateriaStats;

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.BeginGroup();
            if (ImGui.Checkbox("Show Total##materiaStatsTooltipTweak", ref Config.Total)) {
                if (!Config.Total && !Config.Delta) {
                    Config.Delta = true;
                }

                hasChanged = true;
            }
            if (ImGui.Checkbox("Show Delta##materiaStatsTooltipTweak", ref Config.Delta)) {
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
                    
                var text = "Simplified Combined Display";
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (groupSize.Y / 2) - (textSize.Y / 2));
                hasChanged |= ImGui.Checkbox($"{text}##materiaStatSTooltipTweak", ref Config.SimpleCombined);
                ImGui.SetCursorPosY(y);
            }
                
            hasChanged |= ImGui.Checkbox("Colour Value##materiaStatsTooltipTweak", ref Config.Colour);
        };

        public IEnumerable<TooltipTweaks.ItemTooltip.TooltipField> Fields() {
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param0;
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param1;
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param2;
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param3;
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param4;
            yield return TooltipTweaks.ItemTooltip.TooltipField.Param5;
        }
        
        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {
            
            
            if (!(Config.Delta || Config.Total == false)) Config.Total = true; // Config invalid check
            try {
                var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow(itemInfo.ItemId);
                if (item == null) return;
                if (item.MateriaSlotCount == 0) return;
                var itemLevel = CustomSheet.GetSheet<ItemLevel>().GetRow(item.LevelItem.Row);
                if (itemLevel == null) return;
                var baseParams = new Dictionary<uint, BaseParam>();
                var baseParamDeltas = new Dictionary<uint, int>();
                var baseParamOriginal = new Dictionary<uint, int>();
                var baseParamLimits = new Dictionary<uint, int>();
                foreach (var bp in item.UnkStruct60) {
                    if (bp.BaseParam == 0) continue;
                    var param = CustomSheet.GetSheet<BaseParam>().GetRow(bp.BaseParam);
                    if (param == null) continue;
                    baseParamDeltas.Add(param.RowId, 0);
                    baseParamOriginal.Add(param.RowId, bp.BaseParamValue);
                    baseParamLimits.Add(param.RowId, (int) Math.Ceiling(itemLevel.BaseParam[param.RowId] * (param.EquipSlotCategoryPct[item.EquipSlotCategory.Row] / 100f)) );
                    baseParams.Add(param.RowId, param);
                }

                if (itemInfo.IsHQ) {
                    foreach (var bp in item.UnkStruct74) {
                        if (bp.BaseParamSpecial == 0) continue;
                        var param = CustomSheet.GetSheet<BaseParam>().GetRow(bp.BaseParamSpecial);
                        if (param == null) continue;
                        if (baseParamOriginal.ContainsKey(param.RowId)) baseParamOriginal[param.RowId] += bp.BaseParamValueSpecial;
                    }
                }

                if (baseParamDeltas.Count == 0) return;
                
                foreach (var (materiaId, level) in itemInfo.Materia()) {
                    if (level >= 10) continue;
                    var materia = PluginInterface.Data.Excel.GetSheet<Materia>().GetRow(materiaId);
                    if (materia == null) continue;
                    if (materia.BaseParam.Row == 0) continue;
                    if (!baseParamDeltas.ContainsKey(materia.BaseParam.Row)) continue;
                    baseParamDeltas[materia.BaseParam.Row] += materia.Value[level];
                }
                foreach (var bp in baseParamDeltas) {
                    var param = baseParams[bp.Key];
                    if (bp.Value == 0) continue;

                    foreach (var field in Fields()) {
                        var data = tooltip[field];
                        if (data == null) continue;
                        
                        if (data.TextValue.Contains(param.Name)) {
                            data.Payloads.Add(new TextPayload($" ["));
                            var totalValue = baseParamOriginal[bp.Key] + bp.Value;
                            var deltaValue = bp.Value;
                            var exceedLimit = false;
                            if (totalValue > baseParamLimits[bp.Key]) {
                                exceedLimit = true;
                                totalValue = baseParamLimits[bp.Key];
                                deltaValue = baseParamLimits[bp.Key] - baseParamOriginal[bp.Key];
                            }
                            if (Config.Delta) {
                                if (!(Config.Total && Config.SimpleCombined)) data.Payloads.Add(new TextPayload($"+"));
                                if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, (ushort) (exceedLimit ? 14 : 500)));
                                data.Payloads.Add(new TextPayload($"{deltaValue}"));
                                if (Config.Colour && !Config.Total) data.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                                if (Config.Total && !Config.SimpleCombined) {
                                    data.Payloads.Add(new TextPayload("="));
                                } else if (Config.Total) {
                                    data.Payloads.Add(new TextPayload(" "));
                                }
                            }
                            if (Config.Total) {
                                if (Config.Colour) data.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, (ushort) (exceedLimit ? 14 : 500)));
                                data.Payloads.Add(new TextPayload($"{totalValue}"));
                                if (Config.Colour) data.Payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                            }
                            
                            data.Payloads.Add(new TextPayload("]"));

                            tooltip[field] = data;
                        }

                    }
                }

            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }

        }
    }
}
