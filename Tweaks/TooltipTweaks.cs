using Dalamud.Hooking;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public TooltipTweaks.Config TooltipTweaks = new TooltipTweaks.Config();
    }

    public class TooltipTweaks : Tweak {

        private readonly uint[] desynthInDescription = {46, 56, 65, 66, 67, 68, 69, 70, 71, 72};
        
        public class Config {
            public bool EnableDurability;
            public bool EnableSpiritbond;
            public bool TrailingZeros = true;
            public bool ShowDesynthSkill;
            public bool EnableCopyItemName;
            public bool ShowAcquiredStatus = false;
            #if DEBUG
            public bool ShowItemID;
            #endif
            public bool FoodStats;
            public bool FoodStatsHighlight = true;
        }

        public override bool DrawConfig() {
            if (!Enabled) return base.DrawConfig();
            var change = false;
            
            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {
                change = ImGui.Checkbox("Precise Durability", ref PluginConfig.TooltipTweaks.EnableDurability);
                change = ImGui.Checkbox("Precise Spiritbond", ref PluginConfig.TooltipTweaks.EnableSpiritbond) || change;
                ImGui.Indent(20);
                change = ImGui.Checkbox("Trailing Zeros", ref PluginConfig.TooltipTweaks.TrailingZeros) || change;
                ImGui.Indent(-20);
#if DEBUG
                change = ImGui.Checkbox("Show Item ID", ref PluginConfig.TooltipTweaks.ShowItemID) || change;
#endif
                change = ImGui.Checkbox("Show Desynth Skill", ref PluginConfig.TooltipTweaks.ShowDesynthSkill) || change;
                change = ImGui.Checkbox("Show exact food stats", ref PluginConfig.TooltipTweaks.FoodStats) || change;
                if (PluginConfig.TooltipTweaks.FoodStats) {
                    ImGui.Indent(20);
                    change = ImGui.Checkbox("Highlight active value", ref PluginConfig.TooltipTweaks.FoodStatsHighlight) || change;
                    ImGui.Indent(-20);
                }
                change = ImGui.Checkbox("CTRL-C to copy hovered item.", ref PluginConfig.TooltipTweaks.EnableCopyItemName) || change;
                ImGui.TreePop();
            }
            
            return change;
        }

        public override string Name => "Tooltip Tweaks";

        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);

        private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
        
        private delegate ulong GetBaseParam(IntPtr playerAddress, uint baseParamId);
        private Hook<TooltipDelegate> tooltipHook;
        private Hook<ItemHoveredDelegate> itemHoveredHook;

        private IntPtr tooltipAddress;
        private IntPtr itemHoveredAddress;
        private IntPtr playerStaticAddress;
        private IntPtr getBaseParamAddress;

        private GetBaseParam getBaseParam;
        
        private ushort lastSpiritbond;
        private ushort lastDurability;

        public override void Setup() {
            if (Ready) return;
            try {
                if (tooltipAddress == IntPtr.Zero) {
                    tooltipAddress = PluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 ??");
                }

                if (itemHoveredAddress == IntPtr.Zero) {
                    itemHoveredAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 B4 24 ?? ?? ?? ?? 48 89 BC 24 ?? ?? ?? ?? 48 8B 7D A7");
                }

                if (playerStaticAddress == IntPtr.Zero) {
                    playerStaticAddress = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
                }

                if (getBaseParamAddress == IntPtr.Zero) {
                    getBaseParamAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 44 8B C0 33 D2 48 8B CB E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D");
                    getBaseParam = Marshal.GetDelegateForFunctionPointer<GetBaseParam>(getBaseParamAddress);
                }

                if (tooltipAddress == IntPtr.Zero || itemHoveredAddress == IntPtr.Zero) {
                    PluginLog.LogError($"Failed to setup {GetType().Name}: Failed to find required functions.");
                    return;
                }

                Ready = true;

            } catch (Exception ex) {
                PluginLog.LogError($"Failed to setup {this.GetType().Name}: {ex.Message}");
            }

        }

        public override unsafe void Enable() {
            if (!Ready) return;
            tooltipHook ??= new Hook<TooltipDelegate>(tooltipAddress, new TooltipDelegate(TooltipDetour));
            itemHoveredHook ??= new Hook<ItemHoveredDelegate>(itemHoveredAddress, new ItemHoveredDelegate(ItemHoveredDetour));
            tooltipHook?.Enable();
            itemHoveredHook?.Enable();
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnOnUpdateEvent;
            Enabled = true;
        }

        private void FrameworkOnOnUpdateEvent(Framework framework) {

            if (PluginConfig.TooltipTweaks.EnableCopyItemName) {
                if (PluginInterface.Framework.Gui.HoveredItem != 0 && PluginInterface.ClientState.KeyState[0x11] && PluginInterface.ClientState.KeyState[0x43]) {
                    // CTRL + C
                    var id = PluginInterface.Framework.Gui.HoveredItem;
                    if (id < 2000000) {
                        id %= 500000;
                        var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);
                        if (item != null) {
                            System.Windows.Forms.Clipboard.SetText(item.Name);
                            PluginInterface.ClientState.KeyState[0x43] = false;
                        }
                    }
                }
            }
        }

        private unsafe byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7) {
            var ret = itemHoveredHook.Original(a1, a2, containerId, slotId, a5, slotIdInt, a7);
            lastSpiritbond = *(ushort*) (a7 + 16);
            lastDurability = *(ushort*) (a7 + 18);
            return ret;
        }

        public enum TooltipField : byte {
            ItemName,
            GlamourName,
            ItemUiCategory,
            ItemDescription = 13,
            Effects = 16,
            DurabilityPercent = 28,
            SpiritbondPercent = 30,
            ExtractableProjectableDesynthesizable = 35,
            ControlsDisplay = 64,
        }

        private readonly Dictionary<TooltipField,(int size, IntPtr alloc)> tooltipAllocations = new Dictionary<TooltipField, (int size, IntPtr alloc)>();

        private unsafe SeString ReadTooltipField(byte*** tooltipBase, TooltipField field) {
            return Plugin.Common.ReadSeString(*(tooltipBase + 4) + (byte) field);
        }

        private unsafe void WriteTooltipField(byte*** tooltipBase, TooltipField field, SeString value) {

            IntPtr alloc = IntPtr.Zero;
            var size = value.Encode().Length;
            if (tooltipAllocations.ContainsKey(field)) {
                var ta = tooltipAllocations[field];
                if (ta.size < size + 128) {
                    Marshal.FreeHGlobal(ta.alloc);
                    tooltipAllocations.Remove(field);
                } else {
                    alloc = ta.alloc;
                }
            }

            if (alloc == IntPtr.Zero) {
                var allocSize = 64;
                while (allocSize < size + 128) allocSize *= 2;
                alloc = Marshal.AllocHGlobal(allocSize);
                tooltipAllocations.Add(field, (allocSize, alloc));
            }

            Plugin.Common.WriteSeString(*(tooltipBase + 4) + (byte) field, alloc, value);
        }

        private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** tooltipBase) {

            try {
#if DEBUG
            PluginLog.Log("Tooltip Address: " + ((ulong) *(tooltipBase + 4)).ToString("X"));
#endif
                if (PluginConfig.TooltipTweaks.EnableDurability) {
                    var seStr = new SeString(new List<Payload>() {new TextPayload((lastDurability / 300f).ToString(PluginConfig.TooltipTweaks.TrailingZeros ? "F2" : "0.##") + "%")});
                    WriteTooltipField(tooltipBase, TooltipField.DurabilityPercent, seStr);
                }

                if (PluginConfig.TooltipTweaks.EnableSpiritbond) {
                    var seStr = new SeString(new List<Payload>() {new TextPayload((lastSpiritbond / 100f).ToString(PluginConfig.TooltipTweaks.TrailingZeros ? "F2" : "0.##") + "%")});
                    WriteTooltipField(tooltipBase, TooltipField.SpiritbondPercent, seStr);
                }

#if DEBUG
            if (PluginConfig.TooltipTweaks.ShowItemID) {
                var id = PluginInterface.Framework.Gui.HoveredItem;
                if (id < 2000000) { 
                    id %= 500000;
                }

                var seStr = ReadTooltipField(tooltipBase, TooltipField.ItemUiCategory);
                seStr.Payloads.Add(new TextPayload($"  ({id})"));
                WriteTooltipField(tooltipBase, TooltipField.ItemUiCategory, seStr);
            }
#endif

                if (PluginConfig.TooltipTweaks.EnableCopyItemName) {
                    var seStr = ReadTooltipField(tooltipBase, TooltipField.ControlsDisplay);
                    seStr.Payloads.Add(new TextPayload("\nCtrl+C  Copy item name"));
                    WriteTooltipField(tooltipBase, TooltipField.ControlsDisplay, seStr);
                }

                if (PluginConfig.TooltipTweaks.ShowDesynthSkill) {
                    var id = PluginInterface.Framework.Gui.HoveredItem;
                    if (id < 2000000) {
                        id %= 500000;

                        var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);
                        if (item != null && item.Desynth > 0) {
                            var classJobOffset = 2 * (int) (item.ClassJobRepair.Row - 8);
                            var desynthLevel = *(ushort*) (playerStaticAddress + (0x692 + classJobOffset)) / 100f;

                            var useDescription = desynthInDescription.Contains(item.ItemSearchCategory.Row);

                            var seStr = ReadTooltipField(tooltipBase, useDescription ? TooltipField.ItemDescription : TooltipField.ExtractableProjectableDesynthesizable);



                            if (seStr.Payloads.Last() is TextPayload textPayload) {
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row},00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                                textPayload.Text = textPayload.Text.Replace($"{item.LevelItem.Row}.00", $"{item.LevelItem.Row} ({desynthLevel:F0})");
                                WriteTooltipField(tooltipBase, useDescription ? TooltipField.ItemDescription : TooltipField.ExtractableProjectableDesynthesizable, seStr);
                            }

                        }
                    }
                }

                if (PluginConfig.TooltipTweaks.FoodStats) {
                    var id = PluginInterface.Framework.Gui.HoveredItem;
#if DEBUG
                PluginLog.Log($"ID: {id}");
#endif
                    if (id < 2000000) {
                        var hq = id >= 500000;
                        id %= 500000;
                        var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);

                        var action = item.ItemAction?.Value;
#if DEBUG
                    if (action != null) { PluginLog.Log($"ActionType: {action.Type}"); }
#endif
                        if (action != null && (action.Type == 844 || action.Type == 845)) {

                            var itemFood = PluginInterface.Data.Excel.GetSheet<ItemFood>().GetRow(hq ? action.DataHQ[1] : action.Data[1]);
                            if (itemFood != null) {
                                var payloads = new List<Payload>();
                                var hasChange = false;

                                foreach (var bonus in itemFood.UnkStruct1) {
                                    if (bonus.BaseParam == 0) continue;
                                    var param = PluginInterface.Data.Excel.GetSheet<BaseParam>().GetRow(bonus.BaseParam);
                                    var value = hq ? bonus.ValueHQ : bonus.Value;
                                    var max = hq ? bonus.MaxHQ : bonus.Max;
                                    if (bonus.IsRelative) {
                                        hasChange = true;

                                        var currentStat = getBaseParam(playerStaticAddress, bonus.BaseParam);
                                        var relativeAdd = (short) (currentStat * (value / 100f));
                                        var change = relativeAdd > max ? max : relativeAdd;

                                        if (payloads.Count > 0) payloads.Add(new TextPayload("\n"));

                                        payloads.Add(new TextPayload($"{param.Name} +"));

                                        if (PluginConfig.TooltipTweaks.FoodStatsHighlight && change < max) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 500));
                                        payloads.Add(new TextPayload($"{value}%"));
                                        if (change < max) {
                                            if (PluginConfig.TooltipTweaks.FoodStatsHighlight) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                                            payloads.Add(new TextPayload($" (Current "));
                                            if (PluginConfig.TooltipTweaks.FoodStatsHighlight) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 500));
                                            payloads.Add(new TextPayload($"{change}"));
                                            if (PluginConfig.TooltipTweaks.FoodStatsHighlight) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                                            payloads.Add(new TextPayload($")"));
                                        }

                                        payloads.Add(new TextPayload(" (Max "));
                                        if (PluginConfig.TooltipTweaks.FoodStatsHighlight && change == max) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 500));
                                        payloads.Add(new TextPayload($"{max}"));
                                        if (PluginConfig.TooltipTweaks.FoodStatsHighlight && change == max) payloads.Add(new UIForegroundPayload(PluginInterface.Data, 0));
                                        payloads.Add(new TextPayload(")"));
                                    } else {
                                        if (payloads.Count > 0) payloads.Add(new TextPayload("\n"));
                                        payloads.Add(new TextPayload($"{param.Name} +{value}"));
                                    }
                                }

                                if (payloads.Count > 0 && hasChange) {
#if DEBUG
                                PluginLog.Log("Rewriting Food Effects");
#endif
                                    var seStr = new SeString(payloads);
                                    WriteTooltipField(tooltipBase, TooltipField.Effects, seStr);
                                }

                            }

                        }


                    }
                }
            } catch (Exception ex) {
                PluginLog.LogError(ex.ToString());
            }


            return tooltipHook.Original(a1, a2, tooltipBase);
        }

        public override void Disable() {
            tooltipHook?.Disable();
            itemHoveredHook?.Disable();
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnOnUpdateEvent;
            Enabled = false;
        }

        public override void Dispose() {
            tooltipHook?.Dispose();
            itemHoveredHook?.Dispose();
            foreach (var f in tooltipAllocations) {
                Marshal.FreeHGlobal(f.Value.alloc);
            }
            tooltipAllocations.Clear();

            Enabled = false;
            Ready = false;
        }
    }
}
