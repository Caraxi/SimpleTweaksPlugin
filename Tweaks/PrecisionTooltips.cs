using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Plugin;
using ImGuiNET;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public PrecisionTooltips.Config PrecisionTooltips = new PrecisionTooltips.Config();
    }

    public class PrecisionTooltips : Tweak {

        public class Config {
            public bool EnableDurability = true;
            public bool EnableSpiritbond = true;
            public bool TrailingZeros = true;
        }

        public override bool DrawConfig() {
            var change = false;
            change = ImGui.Checkbox("Durability", ref PluginConfig.PrecisionTooltips.EnableDurability) || change;
            change = ImGui.Checkbox("Spiritbond", ref PluginConfig.PrecisionTooltips.EnableSpiritbond) || change;
            change = ImGui.Checkbox("Trailing Zeros", ref PluginConfig.PrecisionTooltips.TrailingZeros) || change;
            return change;
        }

        public override string Name => "Precision Tooltips";

        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);

        private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
        
        private Hook<TooltipDelegate> tooltipHook;
        private Hook<ItemHoveredDelegate> itemHoveredHook;

        private IntPtr tooltipAddress;
        private IntPtr itemHoveredAddress;

        private readonly IntPtr allocSpiritbond = Marshal.AllocHGlobal(32);
        private readonly IntPtr allocDurability = Marshal.AllocHGlobal(32);

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

            Enabled = true;
        }

        private unsafe byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7) {
            var ret = itemHoveredHook.Original(a1, a2, containerId, slotId, a5, slotIdInt, a7);
            lastSpiritbond = *(ushort*) (a7 + 16);
            lastDurability = *(ushort*) (a7 + 18);
            return ret;
        }

        private unsafe void ReplacePercentage(byte** startPtr, IntPtr alloc, double percent) {
            var start = *(startPtr);
            if (start == (byte*) alloc) return;
            var overwrite = ReadString(start);
            if (overwrite == "???%") return;
            overwrite = percent.ToString(PluginConfig.PrecisionTooltips.TrailingZeros ? "F2" : "0.##") + "%";
            WriteString((byte*)alloc, overwrite, true);
            *startPtr = (byte*)alloc;
        }

        private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** a3) {
            if (PluginConfig.PrecisionTooltips.EnableDurability) ReplacePercentage(*(a3+4) + 28, allocDurability, lastDurability / 300.0);
            if (PluginConfig.PrecisionTooltips.EnableSpiritbond) ReplacePercentage(*(a3+4) + 30, allocSpiritbond, lastSpiritbond / 100.0);
            return tooltipHook.Original(a1, a2, a3);
        }

        // https://git.sr.ht/~jkcclemens/GoodMemory/tree/master/GoodMemory/Plugin.cs
        private unsafe string ReadString(byte* ptr) {
            var offset = 0;
            while (true) {
                var b = *(ptr + offset);
                if (b == 0) {
                    break;
                }
                offset += 1;
            }
            return Encoding.UTF8.GetString(ptr, offset);
        }

        // https://git.sr.ht/~jkcclemens/GoodMemory/tree/master/GoodMemory/Plugin.cs
        private unsafe void WriteString(byte* dst, string s, bool finalise = false) {
            var bytes = Encoding.UTF8.GetBytes(s);
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            if (finalise) {
                *(dst + bytes.Length) = 0;
            }
        }

        public override void Disable() {
            tooltipHook?.Disable();
            itemHoveredHook?.Disable();
            Enabled = false;
        }

        public override void Dispose() {
            tooltipHook?.Dispose();
            itemHoveredHook?.Dispose();
            Marshal.FreeHGlobal(this.allocSpiritbond);
            Marshal.FreeHGlobal(this.allocDurability);
            Enabled = false;
            Ready = false;
        }
    }
}
