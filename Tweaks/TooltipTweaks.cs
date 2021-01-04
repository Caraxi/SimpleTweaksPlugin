using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
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

            public abstract void OnItemTooltip(ItemTooltip tooltip, ItemInfo itemInfo);

        }

        private ItemTooltip tooltip;

        public override string Name => "Tooltip Tweaks";
        private IntPtr tooltipAddress;
        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);
        private Hook<TooltipDelegate> tooltipHook;

        private IntPtr itemHoveredAddress;
        private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
        private Hook<ItemHoveredDelegate> itemHoveredHook;

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

            tooltipHook?.Enable();
            itemHoveredHook?.Enable();

            base.Enable();
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ItemInfo {
            [FieldOffset(0x10)] public ushort SpiritBond;
            [FieldOffset(0x12)] public ushort Durability;
        }

        private ItemInfo hoveredItem;

        private unsafe byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerid, ushort* slotid, IntPtr a5, uint slotidint, IntPtr a7) {
            var returnValue = itemHoveredHook.Original(a1, a2, containerid, slotid, a5, slotidint, a7);
            hoveredItem = *(ItemInfo*) (a7);
            return returnValue;
        }

        public override void Disable() {
            tooltipHook?.Disable();
            itemHoveredHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            tooltipHook?.Dispose();
            itemHoveredHook?.Dispose();
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
