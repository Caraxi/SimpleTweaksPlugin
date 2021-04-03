using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.TweakSystem;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments;
using static SimpleTweaksPlugin.Tweaks.UiAdjustments.Step;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public TooltipTweakConfig TooltipTweaks = new TooltipTweakConfig();
    }

    public partial class TooltipTweakConfig { }
}

namespace SimpleTweaksPlugin.Tweaks {
    public class TooltipTweaks : SubTweakManager<TooltipTweaks.SubTweak> {
        public override bool AlwaysEnabled => true;

        public class ItemTooltip : IDisposable {

            public enum TooltipField : byte {
                ItemName,
                GlamourName,
                ItemUiCategory,
                ItemDescription = 13,
                Effects = 16,
                DurabilityPercent = 28,
                SpiritbondPercent = 30,
                ExtractableProjectableDesynthesizable = 35,
                Param0 = 37,
                Param1 = 38,
                Param2 = 39,
                Param3 = 40,
                Param4 = 41,
                Param5 = 42,
                ControlsDisplay = 64,
            }


            public ItemTooltip(SimpleTweaksPlugin plugin) {
                this.Plugin = plugin;
            }

            private SimpleTweaksPlugin Plugin;
            private unsafe byte*** baseTooltipPointer;
            private readonly Dictionary<TooltipField, (int size, IntPtr alloc)> tooltipAllocations = new Dictionary<TooltipField, (int size, IntPtr alloc)>();
            public unsafe SeString this[TooltipField field] {
                get => Plugin.Common.ReadSeString(*(baseTooltipPointer + 4) + (byte)field);
                set {
                    var alloc = IntPtr.Zero;
                    var size = value.Encode().Length;
                    if (tooltipAllocations.ContainsKey(field)) {
                        var (allocatedSize, intPtr) = tooltipAllocations[field];
                        if (allocatedSize < size + 128) {
                            Marshal.FreeHGlobal(intPtr);
                            tooltipAllocations.Remove(field);
                        } else {
                            alloc = intPtr;
                        }
                    }

                    if (alloc == IntPtr.Zero) {
                        var allocSize = 64;
                        while (allocSize < size + 128) allocSize *= 2;
                        alloc = Marshal.AllocHGlobal(allocSize);
                        tooltipAllocations.Add(field, (allocSize, alloc));
                    }

                    Plugin.Common.WriteSeString(*(baseTooltipPointer + 4) + (byte)field, alloc, value);
                }
            }
            public SeString this[byte field] {
                get => this[(TooltipField) field];
                set => this[(TooltipField) field] = value;
            }

            public unsafe void SetPointer(byte*** ptr) {
                baseTooltipPointer = ptr;
            }

            public void Dispose() {
                foreach (var f in tooltipAllocations) {
                    Marshal.FreeHGlobal(f.Value.alloc);
                }
                tooltipAllocations.Clear();
            }
        }

        public abstract class SubTweak : BaseTweak {
            public override string Key => $"{nameof(TooltipTweaks)}@{base.Key}";
            public virtual void OnItemTooltip(ItemTooltip tooltip, InventoryItem itemInfo) { }
            public virtual unsafe void OnActionTooltip(AddonActionDetail* addonActionDetail, HoveredAction action) { }
        }

        private ItemTooltip tooltip;

        public override string Name => "Tooltip Tweaks";
        private IntPtr tooltipAddress;
        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);
        private Hook<TooltipDelegate> tooltipHook;

        private unsafe delegate IntPtr ActionTooltipDelegate(AddonActionDetail* a1, void* a2, ulong a3);
        private Hook<ActionTooltipDelegate> actionTooltipHook;
        

        private IntPtr itemHoveredAddress;
        private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
        private Hook<ItemHoveredDelegate> itemHoveredHook;
        
        private unsafe delegate void ActionHoveredDelegate(ulong a1, int a2, uint a3, int a4, byte a5);
        private Hook<ActionHoveredDelegate> actionHoveredHook;

        public override void Setup() {

            if (Ready) return;
            try {
                if (tooltipAddress == IntPtr.Zero) {
                    tooltipAddress = PluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 ??");
                }

                if (itemHoveredAddress == IntPtr.Zero) {
                    itemHoveredAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 B4 24 ?? ?? ?? ?? 48 89 BC 24 ?? ?? ?? ?? 48 8B 7D A7");
                }

                if (tooltipAddress == IntPtr.Zero || itemHoveredAddress == IntPtr.Zero) {
                    SimpleLog.Error($"Failed Setup of {GetType().Name}: Failed to find required functions.");
                    return;
                }
                base.Setup();
            } catch (Exception ex) {
                SimpleLog.Error($"{ex}");
            }
        }

        public override unsafe void Enable() {
            if (!Ready) return;
            
            tooltipHook ??= new Hook<TooltipDelegate>(tooltipAddress, new TooltipDelegate(TooltipDetour));
            itemHoveredHook ??= new Hook<ItemHoveredDelegate>(itemHoveredAddress, new ItemHoveredDelegate(ItemHoveredDetour));
            
            
            // 
            actionTooltipHook ??= new Hook<ActionTooltipDelegate>(
                PluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B AA"), 
                new ActionTooltipDelegate(ActionTooltipDetour));
            actionHoveredHook ??= new Hook<ActionHoveredDelegate>(
                PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 83 F8 0F"),
                new ActionHoveredDelegate(ActionHoveredDetour));
            
            tooltipHook?.Enable();
            itemHoveredHook?.Enable();
            actionTooltipHook?.Enable();
            actionHoveredHook?.Enable();

            base.Enable();
        }


        public class HoveredAction {
            public int Category;
            public uint Id;
            public int Unknown3;
            public byte Unknown4;
        }

        private HoveredAction hoveredAction = new HoveredAction();
        private void ActionHoveredDetour(ulong a1, int a2, uint a3, int a4, byte a5) {
            hoveredAction.Category = a2;
            hoveredAction.Id = a3;
            hoveredAction.Unknown3 = a4;
            hoveredAction.Unknown4 = a5;
            actionHoveredHook?.Original(a1, a2, a3, a4, a5);
        }

        private unsafe IntPtr ActionTooltipDetour(AddonActionDetail* addon, void* a2, ulong a3) {
            var retVal = actionTooltipHook.Original(addon, a2, a3);
            try {
                foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                    try {
                        t.OnActionTooltip(addon, hoveredAction);
                    } catch (Exception ex) {
                        Plugin.Error(this, t, ex);
                    }
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            return retVal;
        }
        
        private InventoryItem hoveredItem;
        private unsafe byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerid, ushort* slotid, IntPtr a5, uint slotidint, IntPtr a7) {
            var returnValue = itemHoveredHook.Original(a1, a2, containerid, slotid, a5, slotidint, a7);
            hoveredItem = *(InventoryItem*) (a7);
            return returnValue;
        }

        public override void Disable() {
            tooltipHook?.Disable();
            itemHoveredHook?.Disable();
            actionTooltipHook?.Disable();
            actionHoveredHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            tooltipHook?.Dispose();
            itemHoveredHook?.Dispose();
            actionTooltipHook?.Dispose();
            actionHoveredHook?.Dispose();
            base.Dispose();
        }

        private async void SetControlsSectionHeight(int height) {
            await Task.Delay(5);
            unsafe {
                var heightShort = (ushort)height;
                var tooltipUi = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("ItemDetail", 1);
                if (tooltipUi == null) return;
                var bg = GetResNodeByPath(tooltipUi->RootNode, Child, PrevFinal, Child, Child);
                if (bg != null) bg->Height = heightShort;
            }
        }

        private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** a3) {
            try {
                tooltip ??= new ItemTooltip(Plugin);
                tooltip.SetPointer(a3);
                SimpleLog.Verbose($"TooltipDetour: {(ulong)*(a3+4):X}");
                foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                    try {
                        t.OnItemTooltip(tooltip, hoveredItem);
                    } catch (Exception ex) {
                        Plugin.Error(this, t, ex);
                    }
                }

                if (tooltip[ItemTooltip.TooltipField.ControlsDisplay] != null) {
                    SetControlsSectionHeight(tooltip[ItemTooltip.TooltipField.ControlsDisplay].TextValue.Split('\n').Length * 18 + 8);
                }
            } catch (Exception ex) {
                Plugin.Error(this, ex);
            }

            return tooltipHook.Original(a1, a2, a3);
        }
    }
}
