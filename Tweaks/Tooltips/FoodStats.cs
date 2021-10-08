using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Sheets;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class TooltipTweakConfig {
        public bool ShouldSerializeFoodStatsHighlight() => false;
        public bool FoodStatsHighlight = false;
    }
}

namespace SimpleTweaksPlugin.Tweaks.Tooltips {
    public class FoodStats : TooltipTweaks.SubTweak {
        public override string Name => "Show expected food and potion stats";
        public override string Description => "Calculates the stat results a consumable will have based on your current stats.";

        private IntPtr playerStaticAddress;
        private IntPtr getBaseParamAddress;
        private delegate ulong GetBaseParam(IntPtr playerAddress, uint baseParamId);
        private GetBaseParam getBaseParam;

        public class Configs : TweakConfig {
            public bool Highlight = false;
        }
        
        public Configs Config { get; private set; }
        

        public override void Setup() {
            try {
                if (getBaseParamAddress == IntPtr.Zero) {
                    getBaseParamAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 8B C0 33 D2 48 8B CB E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D");
                    getBaseParam = Marshal.GetDelegateForFunctionPointer<GetBaseParam>(getBaseParamAddress);
                }

                if (playerStaticAddress == IntPtr.Zero) {
                    playerStaticAddress = Service.SigScanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
                }
                base.Setup();
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }
        }

        private ExcelSheet<ExtendedItem> itemSheet;
        private ExcelSheet<ItemFood> foodSheet;
        private ExcelSheet<BaseParam> bpSheet;

        public override void Enable() {
            itemSheet = Service.Data.Excel.GetSheet<ExtendedItem>();
            foodSheet = Service.Data.Excel.GetSheet<ItemFood>();
            bpSheet = Service.Data.Excel.GetSheet<BaseParam>();
            if (itemSheet == null || foodSheet == null || bpSheet == null) return;
            Config = LoadConfig<Configs>() ?? new Configs() { Highlight = PluginConfig.TooltipTweaks.FoodStatsHighlight };
            base.Enable();
        }

        public override void Disable() {
            SaveConfig(Config);
            base.Disable();
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.Checkbox("Highlight Active", ref Config.Highlight);
        };

        public override void OnItemTooltip(TooltipTweaks.ItemTooltip tooltip, InventoryItem itemInfo) {

            var id = Service.GameGui.HoveredItem;

            if (id < 2000000) {
                var hq = id >= 500000;
                id %= 500000;
                var item = itemSheet.GetRow((uint)id);
                if (item == null) return;
                var action = item.ItemAction?.Value;

                if (action != null && action.Type is 844 or 845 or 846) {

                    var itemFood = foodSheet.GetRow(hq ? action.DataHQ[1] : action.Data[1]);
                    if (itemFood != null) {
                        var payloads = new List<Payload>();
                        var hasChange = false;

                        foreach (var bonus in itemFood.UnkStruct1) {
                            if (bonus.BaseParam == 0) continue;
                            var param = bpSheet.GetRow(bonus.BaseParam);
                            if (param == null) continue;
                            var value = hq ? bonus.ValueHQ : bonus.Value;
                            var max = hq ? bonus.MaxHQ : bonus.Max;
                            if (bonus.IsRelative) {
                                hasChange = true;

                                var currentStat = getBaseParam(playerStaticAddress, bonus.BaseParam);
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
                            tooltip[TooltipTweaks.ItemTooltip.TooltipField.Effects] = seStr;
                        }

                    }

                }
            }
        }
    }
}
