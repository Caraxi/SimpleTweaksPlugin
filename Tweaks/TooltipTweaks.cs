using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class TooltipTweaks : SubTweakManager<TooltipTweaks.SubTweak> {
    public override bool AlwaysEnabled => true;

    public static Dictionary<ItemTooltipField, IntPtr> ItemStringPointers = new();

    public abstract class SubTweak : BaseTweak {
        public override string Key => $"{nameof(TooltipTweaks)}@{base.Key}";
        public virtual unsafe void OnActionTooltip(AddonActionDetail* addonActionDetail, HoveredAction action) { }
        public virtual unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) { }

        protected static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field) {
            try {
                var stringAddress = new IntPtr(stringArrayData->StringArray[(int)field]);
                return stringAddress == IntPtr.Zero ? null : MemoryHelper.ReadSeStringNullTerminated(stringAddress);
            } catch {
                return null;
            }
        }

        protected static unsafe void SetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field, SeString seString) {
            try {
                if (!ItemStringPointers.ContainsKey(field)) ItemStringPointers.Add(field, Marshal.AllocHGlobal(4096));
                var bytes = seString.Encode();
                Marshal.Copy(bytes, 0, ItemStringPointers[field], bytes.Length);
                Marshal.WriteByte(ItemStringPointers[field], bytes.Length, 0);
                stringArrayData->StringArray[(int)field] = (byte*)ItemStringPointers[field];
            } catch {
                //
            }
        }

        protected static InventoryItem Item => HoveredItem;
    }

    public override string Name => "Tooltip Tweaks";
    private unsafe delegate IntPtr ActionTooltipDelegate(AddonActionDetail* a1, void* a2, ulong a3);
    private HookWrapper<ActionTooltipDelegate> actionTooltipHook;

    private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
    private HookWrapper<ItemHoveredDelegate> itemHoveredHook;
        
    private delegate void ActionHoveredDelegate(ulong a1, int a2, uint a3, int a4, byte a5);
    private HookWrapper<ActionHoveredDelegate> actionHoveredHook;

    private unsafe delegate void* GenerateItemTooltip(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);
    private HookWrapper<GenerateItemTooltip> generateItemTooltipHook;

    public override unsafe void Enable() {
        if (!Ready) return;

        itemHoveredHook ??= Common.Hook<ItemHoveredDelegate>("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 B4 24 ?? ?? ?? ?? 48 89 BC 24 ?? ?? ?? ?? 48 8B 7D A7", ItemHoveredDetour);
        actionTooltipHook ??= Common.Hook<ActionTooltipDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B AA", ActionTooltipDetour);
        actionHoveredHook ??= Common.Hook<ActionHoveredDelegate>("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 83 F8 0F", ActionHoveredDetour);
        generateItemTooltipHook ??= Common.Hook<GenerateItemTooltip>("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 20", GenerateItemTooltipDetour);

        itemHoveredHook?.Enable();
        actionTooltipHook?.Enable();
        actionHoveredHook?.Enable();
        generateItemTooltipHook?.Enable();

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
        
    public static InventoryItem HoveredItem { get; private set; }

    private unsafe byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerid, ushort* slotid, IntPtr a5, uint slotidint, IntPtr a7) {
        var returnValue = itemHoveredHook.Original(a1, a2, containerid, slotid, a5, slotidint, a7);
        HoveredItem = *(InventoryItem*) (a7);
        return returnValue;
    }

    public override void Disable() {
        itemHoveredHook?.Disable();
        actionTooltipHook?.Disable();
        actionHoveredHook?.Disable();
        generateItemTooltipHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        itemHoveredHook?.Dispose();
        actionTooltipHook?.Dispose();
        actionHoveredHook?.Dispose();
        generateItemTooltipHook?.Dispose();
        foreach (var i in ItemStringPointers.Values) {
            Marshal.FreeHGlobal(i);
        }
        ItemStringPointers.Clear();
        base.Dispose();
    }

    public unsafe void* GenerateItemTooltipDetour(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        try {
            foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                try {
                    t.OnGenerateItemTooltip(numberArrayData, stringArrayData);
                } catch (Exception ex) {
                    Plugin.Error(this, t, ex);
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        return generateItemTooltipHook.Original(addonItemDetail, numberArrayData, stringArrayData);
    }

    public enum ItemTooltipField : byte {
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
}