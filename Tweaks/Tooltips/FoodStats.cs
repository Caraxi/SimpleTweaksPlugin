using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool ShouldSerializeFoodStatsHighlight() => false;
        public bool FoodStatsHighlight = false;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public unsafe class FoodStats : TooltipTweaks.SubTweak {
        public override string Name => "Show expected food and potion stats";
        public override string Description => "Calculates the stat results a consumable will have based on your current stats.";
        
        private IntPtr getBaseParamAddress;
        private delegate ulong GetBaseParam(PlayerState* playerAddress, uint baseParamId);
        private GetBaseParam getBaseParam;

        public class Configs : TweakConfig {
            public bool Highlight;
        }
        
        public Configs Config { get; private set; }

        public override void Setup() {
            try {
                if (getBaseParamAddress == IntPtr.Zero) {
                    getBaseParamAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 8B C0 33 D2 48 8B CB E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D");
                    getBaseParam = Marshal.GetDelegateForFunctionPointer<GetBaseParam>(getBaseParamAddress);
                }
                
                base.Setup();
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }

        private ExcelSheet<ExtendedItem> itemSheet;
        private ExcelSheet<ItemFood> foodSheet;
        private ExcelSheet<BaseParam> bpSheet;

        private TextPayload potionPercentPayload = new TextPayload("??%");
        private TextPayload potionMaxPayload = new TextPayload("????");
        private TextPayload potionActualValuePayload = new TextPayload("????");

        private SeString hpPotionEffectString;
        private SeString hpPotionCappedEffectString;

        public override void Enable() {
            itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
            foodSheet = Service.Data.Excel.GetSheet<ItemFood>();
            bpSheet = Service.Data.Excel.GetSheet<BaseParam>();
            if (itemSheet == null || foodSheet == null || bpSheet == null) return;
            Config = LoadConfig<Configs>() ?? new Configs();
            BuildPotionEffectStrings();
            base.Enable();
        }

        private void BuildPotionEffectStrings() {
            hpPotionEffectString = new SeString();
            hpPotionCappedEffectString = new SeString();

            var hpEffectString = (SeString) Service.Data.Excel.GetSheet<Addon>()?.GetRow(998)?.Text;

            var removePercent = false;

            foreach (var p in hpEffectString.Payloads) {
                if (p is RawPayload rp) {
                    switch (rp.Data[^2]) {
                        case 2:
                            if(Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(500));
                            hpPotionEffectString.Payloads.Add(potionPercentPayload);
                            if(Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(0));

                            hpPotionEffectString.Payloads.Add(new TextPayload(" ("));
                            if(Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(500));
                            hpPotionEffectString.Payloads.Add(potionActualValuePayload);
                            if(Config.Highlight) hpPotionEffectString.Payloads.Add(new UIForegroundPayload(0));
                            hpPotionEffectString.Payloads.Add(new TextPayload(")"));

                            hpPotionCappedEffectString.Payloads.Add(potionPercentPayload);
                            removePercent = true;
                            break;
                        case 3:
                            hpPotionEffectString.Payloads.Add(potionMaxPayload);

                            if(Config.Highlight) hpPotionCappedEffectString.Payloads.Add(new UIForegroundPayload(500));
                            hpPotionCappedEffectString.Payloads.Add(potionMaxPayload);
                            if(Config.Highlight) hpPotionCappedEffectString.Payloads.Add(new UIForegroundPayload(0));

                            break;
                        default:
                            hpPotionEffectString.Payloads.Add(p);
                            break;
                    }
                } else {
                    if (removePercent) {
                        removePercent = false;
                        if (p is TextPayload tp && tp.Text.StartsWith("%")) {
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

        public override void Disable() {
            SaveConfig(Config);
            base.Disable();
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox(LocString("Highlight Active"), ref Config.Highlight);
            if (hasChanged) BuildPotionEffectStrings();
        };

        public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
            if (Service.ClientState.LocalPlayer == null) return;
            var id = Service.GameGui.HoveredItem;
            if (id < 2000000) {
                var hq = id >= 500000;
                id %= 500000;
                var item = itemSheet.GetRow((uint)id);
                if (item == null) return;
                var action = item.ItemAction?.Value;


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

                    stringArrayData->SetValue((int) TooltipTweaks.ItemTooltipField.Effects, seStr.Encode(), false);
                }


                if (action is { Type: 844 or 845 or 846 }) {
                    var itemFood = foodSheet.GetRow(hq ? action.DataHQ[1] : action.Data[1]);
                    if (itemFood != null) {
                        var payloads = new List<Payload>();
                        var hasChange = false;

                        foreach (var bonus in itemFood.UnkData1) {
                            if (bonus.BaseParam == 0) continue;
                            var param = bpSheet.GetRow(bonus.BaseParam);
                            if (param == null) continue;
                            var value = hq ? bonus.ValueHQ : bonus.Value;
                            var max = hq ? bonus.MaxHQ : bonus.Max;
                            if (bonus.IsRelative) {
                                hasChange = true;

                                var currentStat = getBaseParam(&UIState.Instance()->PlayerState, bonus.BaseParam);
                                var relativeAdd = (short)(currentStat * (value / 100f));
                                var change = relativeAdd > max ? max : relativeAdd;

                                if (payloads.Count > 0) payloads.Add(new TextPayload("\n"));

                                payloads.Add(new TextPayload($"{param.Name} +"));

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
                                payloads.Add(new TextPayload($"{param.Name} +{value}"));
                            }
                        }

                        if (payloads.Count > 0 && hasChange) {
                            var seStr = new SeString(payloads);
                            try {
                                SetTooltipString(stringArrayData, TooltipTweaks.ItemTooltipField.Effects, seStr);
                            } catch (Exception ex) {
                                Plugin.Error(this, ex);
                            }
                            
                        }
                    }

                }
            }
        }
    }
}
