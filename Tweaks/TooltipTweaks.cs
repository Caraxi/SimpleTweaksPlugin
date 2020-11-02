using Dalamud.Hooking;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud;
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
            public bool EnableDurability = true;
            public bool EnableSpiritbond = true;
            public bool TrailingZeros = true;
            public bool ShowDesynthSkill = true;
            public bool EnableCopyItemName = true;
            public bool ShowAcquiredStatus = false;
            #if DEBUG
            public bool ShowItemID = false;
            #endif
            public bool AlwaysShowGlamourName = false;
        }

        public override bool DrawConfig() {
            if (!Enabled) return base.DrawConfig();
            var change = false;
            
            if (ImGui.TreeNode($"{Name}###{GetType().Name}settingsNode")) {
                change = ImGui.Checkbox("Precise Durability", ref PluginConfig.TooltipTweaks.EnableDurability) || change;
                change = ImGui.Checkbox("Precise Spiritbond", ref PluginConfig.TooltipTweaks.EnableSpiritbond) || change;
                #if DEBUG
                change = ImGui.Checkbox("Show Item ID", ref PluginConfig.TooltipTweaks.ShowItemID) || change;
                #endif
                ImGui.Indent(20);
                change = ImGui.Checkbox("Trailing Zeros", ref PluginConfig.TooltipTweaks.TrailingZeros) || change;
                ImGui.Indent(-20);
                change = ImGui.Checkbox("Show Desynth Skill", ref PluginConfig.TooltipTweaks.ShowDesynthSkill) || change;
                change = ImGui.Checkbox("Always show glamour name", ref PluginConfig.TooltipTweaks.AlwaysShowGlamourName) || change;
                change = ImGui.Checkbox("CTRL-C to copy hovered item.", ref PluginConfig.TooltipTweaks.EnableCopyItemName) || change;
                ImGui.TreePop();
            }
            
            return change;
        }

        public override string Name => "Tooltip Tweaks";

        private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);

        private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
        
        private Hook<TooltipDelegate> tooltipHook;
        private Hook<ItemHoveredDelegate> itemHoveredHook;

        private IntPtr tooltipAddress;
        private IntPtr itemHoveredAddress;
        private IntPtr playerStaticAddress;

        private readonly IntPtr allocSpiritbond = Marshal.AllocHGlobal(32);
        private readonly IntPtr allocDurability = Marshal.AllocHGlobal(32);
        private readonly IntPtr allocDesynthSkill = Marshal.AllocHGlobal(512);
        private readonly IntPtr allocControlDisplay = Marshal.AllocHGlobal(512);
        private readonly IntPtr allocItemName = Marshal.AllocHGlobal(512);
        #if DEBUG
        private readonly IntPtr allocItemId = Marshal.AllocHGlobal(512);
        #endif

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

        private unsafe void ReplacePercentage(byte** startPtr, IntPtr alloc, double percent) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*) alloc) return;
            var overwrite = ReadString(start);
            if (overwrite == "???%") return;
            overwrite = percent.ToString(PluginConfig.TooltipTweaks.TrailingZeros ? "F2" : "0.##") + "%";
            WriteString((byte*)alloc, overwrite, true);
            *startPtr = (byte*)alloc;
        }

        private unsafe void ReplaceText(byte** startPtr, IntPtr alloc, string find, string replace) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*) alloc) return;
            var overwrite = ReadString(start);
            if (!overwrite.Contains(find)) return;
            overwrite = overwrite.Replace(find, replace);
            WriteString((byte*)alloc, overwrite, true);
            *startPtr = (byte*)alloc;
        }

        private unsafe void AppendText(byte** startPtr, IntPtr alloc, string text) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*) alloc) return;
            var overwrite = ReadString(start);
            overwrite += text;
            WriteString((byte*)alloc, overwrite, true);
            *startPtr = (byte*)alloc;
        }

        private unsafe void AppendSeString(byte** startPtr, IntPtr alloc, SeString append) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*)alloc) return;
            var overwrite = ReadSeString(start);
            overwrite.Payloads.AddRange(append.Payloads);
            WriteSeString((byte*) alloc, overwrite);
            *startPtr = (byte*) alloc;
        }

        private unsafe void WriteSeString(byte** startPtr, IntPtr alloc, SeString seString) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*)alloc) return;
            WriteSeString((byte*) alloc, seString);
            *startPtr = (byte*) alloc;
        }

        private unsafe SeString ReadSeString(byte** startPtr) {
            if (startPtr == null) return null;
            var start = *(startPtr);
            if (start == null) return null;
            return ReadSeString(start);
        }



        private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** a3) {
#if DEBUG
            PluginLog.Log("Tooltip Address: " + ((ulong) *(a3 + 4)).ToString("X"));
#endif
            if (PluginConfig.TooltipTweaks.EnableDurability) ReplacePercentage(*(a3+4) + 28, allocDurability, lastDurability / 300.0);
            if (PluginConfig.TooltipTweaks.EnableSpiritbond) ReplacePercentage(*(a3+4) + 30, allocSpiritbond, lastSpiritbond / 100.0);
            #if DEBUG
            if (PluginConfig.TooltipTweaks.ShowItemID) {
                var id = PluginInterface.Framework.Gui.HoveredItem;
                if (id < 2000000) { 
                    id %= 500000;
                }
                AppendText(*(a3 + 4) + 2, allocItemId, $"  ({id})");
            }
            #endif
            if (PluginConfig.TooltipTweaks.EnableCopyItemName) {
                AppendText(*(a3 + 4) + 0x40, allocControlDisplay, "　Ctrl+C  Copy item name");
            }
            
            if (PluginConfig.TooltipTweaks.ShowDesynthSkill) {
                var id = PluginInterface.Framework.Gui.HoveredItem;
                if (id < 2000000) {
                    id %= 500000;
                    
                    var item = PluginInterface.Data.Excel.GetSheet<Item>().GetRow((uint) id);
                    if (item != null && item.Unknown36 > 0) {
                        PluginLog.Log($"Desynthable: {item.Name}");
                        var classJobOffset = 2 * (int) (item.ClassJobRepair.Row - 8);
                        var desynthLevel = *(ushort*) (playerStaticAddress + (0x692 + classJobOffset)) / 100f;

                        var useDescription = desynthInDescription.Contains(item.ItemSearchCategory.Row);




                        switch (PluginInterface.ClientState.ClientLanguage) {
                            case ClientLanguage.Japanese:
                                ReplaceText(*(a3 + 4) + (useDescription ? 0xD : 0x23), allocDesynthSkill, $"分解適正スキル:{item.LevelItem.Row:F0}.00", $"分解適正スキル:{item.LevelItem.Row} ({desynthLevel:F0})");
                                break;
                            case ClientLanguage.English:
                                ReplaceText(*(a3 + 4) + (useDescription ? 0xD : 0x23), allocDesynthSkill, $"Desynthesizable: {item.LevelItem.Row:F0}.00", $"Desynthable: {item.LevelItem.Row} ({desynthLevel:F0})");
                                break;
                            case ClientLanguage.German:
                                ReplaceText(*(a3 + 4) + (useDescription ? 0xD : 0x23), allocDesynthSkill, $"Verwertung: {item.LevelItem.Row},00", $"Verwertung: {item.LevelItem.Row} ({desynthLevel:F0})");
                                break;
                            case ClientLanguage.French:
                                ReplaceText(*(a3 + 4) + (useDescription ? 0xD : 0x23), allocDesynthSkill, $"Recyclage: ✓ [{item.LevelItem.Row},00]", $"\nRecyclage: ✓ {item.LevelItem.Row} ({desynthLevel:F0})");
                                break;
                        }
                        
                        
                    }
                }
            }
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

        private unsafe SeString ReadSeString(byte* ptr) {
            var offset = 0;
            while (true) {
                var b = *(ptr + offset);
                if (b == 0) {
                    break;
                }
                offset += 1;
            }

            var bytes = new byte[offset];
            Marshal.Copy(new IntPtr(ptr), bytes, 0, offset);

            return PluginInterface.SeStringManager.Parse(bytes);
        }

        private unsafe void WriteString(byte* dst, string s, bool finalise = false) {
            var bytes = Encoding.UTF8.GetBytes(s);
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            if (finalise) {
                *(dst + bytes.Length) = 0;
            }
        }

        private unsafe void WriteSeString(byte* dst, SeString s) {
            var bytes = s.Encode();
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }
            *(dst + bytes.Length) = 0;
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
            Marshal.FreeHGlobal(this.allocSpiritbond);
            Marshal.FreeHGlobal(this.allocDurability);
            Marshal.FreeHGlobal(this.allocDesynthSkill);
            Marshal.FreeHGlobal(this.allocControlDisplay);
            Marshal.FreeHGlobal(this.allocItemName);
#if DEBUG
            Marshal.FreeHGlobal(this.allocItemId);
#endif
            Enabled = false;
            Ready = false;
        }
    }
}
