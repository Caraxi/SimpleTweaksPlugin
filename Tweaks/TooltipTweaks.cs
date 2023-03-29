using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using Dalamud.Memory.Exceptions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public class TooltipTweaks : SubTweakManager<TooltipTweaks.SubTweak> {
    public override bool AlwaysEnabled => true;

    public static Dictionary<ItemTooltipField, IntPtr> ItemStringPointers = new();
    public static Dictionary<ActionTooltipField, IntPtr> ActionStringPointers = new();

    public abstract class SubTweak : BaseTweak {
        public override string Key => $"{nameof(TooltipTweaks)}@{base.Key}";
        public virtual unsafe void OnActionTooltip(AtkUnitBase* addonActionDetail, HoveredActionDetail action) { }
        public virtual unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) { }
        public virtual unsafe void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) { }

        protected static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field) => GetTooltipString(stringArrayData, (int)field); 
        protected static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ActionTooltipField field) => GetTooltipString(stringArrayData, (int)field); 
        
        protected static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, int field) {
            try {
                if (stringArrayData->AtkArrayData.Size <= field) 
                    throw new IndexOutOfRangeException($"Attempted to get Index#{field} ({field}) but size is only {stringArrayData->AtkArrayData.Size}");

                var stringAddress = new IntPtr(stringArrayData->StringArray[field]);
                return stringAddress == IntPtr.Zero ? null : MemoryHelper.ReadSeStringNullTerminated(stringAddress);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                return new SeString();
            }
        }

        protected static unsafe void SetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field, SeString seString) {
            try {
                seString ??= new SeString();
                
                if (stringArrayData->AtkArrayData.Size <= (int)field) 
                    throw new IndexOutOfRangeException($"Attempted to set Index#{(int)field} ({field}) but size is only {stringArrayData->AtkArrayData.Size}");

                if (!ItemStringPointers.ContainsKey(field)) {
                    var newAlloc = Marshal.AllocHGlobal(4096);
                    if (newAlloc == nint.Zero) {
                        throw new MemoryAllocationException("Failed to allocate memory.");
                    }
                    ItemStringPointers.Add(field, newAlloc);
                }
                
                var bytes = seString.Encode();
                if (bytes.Length > 4095) throw new Exception($"Attempted to set a string of length {bytes.Length} to {field}. Max size is 4096");
                Marshal.Copy(bytes, 0, ItemStringPointers[field], bytes.Length);
                Marshal.WriteByte(ItemStringPointers[field], bytes.Length, 0);
                stringArrayData->StringArray[(int)field] = (byte*)ItemStringPointers[field];
            } catch (Exception ex) {
                throw;
            }
        }
        
        protected static unsafe void SetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ActionTooltipField field, SeString seString) {
            try {
                seString ??= new SeString();
                
                if (stringArrayData->AtkArrayData.Size <= (int)field) {
                    throw new IndexOutOfRangeException($"Attempted to set Index#{(int)field} ({field}) but size is only {stringArrayData->AtkArrayData.Size}");
                }
                    
                if (!ActionStringPointers.ContainsKey(field)) {
                    var newAlloc = Marshal.AllocHGlobal(4096);
                    if (newAlloc == nint.Zero) {
                        throw new MemoryAllocationException("Failed to allocate memory.");
                    }
                    ActionStringPointers.Add(field, newAlloc);
                }
                var bytes = seString.Encode();
                if (bytes.Length > 4095) throw new Exception($"Attempted to set a string of length {bytes.Length} to {field}. Max size is 4096");
                Marshal.Copy(bytes, 0, ActionStringPointers[field], bytes.Length);
                Marshal.WriteByte(ActionStringPointers[field], bytes.Length, 0);
                stringArrayData->StringArray[(int)field] = (byte*)ActionStringPointers[field];
            } catch {
                throw;
            }
        }

        protected static InventoryItem Item => HoveredItem;
        protected static HoveredActionDetail Action => HoveredAction;
        protected static uint LoadedItem => LastLoadedItem;
    }

    public override string Name => "Tooltip Tweaks";
    public override uint Version => 2;
    private unsafe delegate IntPtr ActionTooltipDelegate(AtkUnitBase* a1, void* a2, ulong a3);
    private HookWrapper<ActionTooltipDelegate> actionTooltipHook;

    private unsafe delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);
    private HookWrapper<ItemHoveredDelegate> itemHoveredHook;
        
    private delegate void ActionHoveredDelegate(ulong a1, int a2, uint a3, int a4, byte a5);
    private HookWrapper<ActionHoveredDelegate> actionHoveredHook;

    private unsafe delegate void* GenerateItemTooltip(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);
    private HookWrapper<GenerateItemTooltip> generateItemTooltipHook;
    
    private unsafe delegate void* GenerateActionTooltip(AtkUnitBase* addonActionDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);
    private HookWrapper<GenerateActionTooltip> generateActionTooltipHook;

    private unsafe delegate void* GetItemRowDelegate(uint itemId);
    private HookWrapper<GetItemRowDelegate> getItemRowHook;

    public override void Setup() {
        AddChangelog(Changelog.UnreleasedVersion, "Added additional protections to attempt to reduce crashing. Please report any crashes you believe may be related to tooltips.");
        base.Setup();
    }

    public override unsafe void Enable() {
        if (!Ready) return;

        itemHoveredHook ??= Common.Hook<ItemHoveredDelegate>("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 B4 24 ?? ?? ?? ?? 48 89 BC 24 ?? ?? ?? ?? 48 8B 7C 24", ItemHoveredDetour);
        actionTooltipHook ??= Common.Hook<ActionTooltipDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC 20 48 8B AA", ActionTooltipDetour);
        actionHoveredHook ??= Common.Hook<ActionHoveredDelegate>("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 83 F8 0F", ActionHoveredDetour);
        generateItemTooltipHook ??= Common.Hook<GenerateItemTooltip>("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 20", GenerateItemTooltipDetour);
        generateActionTooltipHook ??= Common.Hook<GenerateActionTooltip>("E8 ?? ?? ?? ?? 48 8B D5 48 8B CF E8 ?? ?? ?? ?? 41 8D 45 FF 83 F8 01 77 6D", GenerateActionTooltipDetour);
        getItemRowHook ??= Common.Hook<GetItemRowDelegate>("E8 ?? ?? ?? ?? 4C 8B F8 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B D0", GetItemRowDetour);

        itemHoveredHook?.Enable();
        actionTooltipHook?.Enable();
        actionHoveredHook?.Enable();
        generateItemTooltipHook?.Enable();
        generateActionTooltipHook?.Enable();
        getItemRowHook?.Enable();

        base.Enable();
    }

    private unsafe void* GetItemRowDetour(uint itemId) {
        LastLoadedItem = itemId;
        return getItemRowHook.Original(itemId);
    }

    public class HoveredActionDetail {
        public int Category;
        public uint Id;
        public int Unknown3;
        public byte Unknown4;
    }

    public static readonly HoveredActionDetail HoveredAction = new HoveredActionDetail();


    public static uint LastLoadedItem { get; private set; }
    
    private void ActionHoveredDetour(ulong a1, int a2, uint a3, int a4, byte a5) {
        HoveredAction.Category = a2;
        HoveredAction.Id = a3;
        HoveredAction.Unknown3 = a4;
        HoveredAction.Unknown4 = a5;
        actionHoveredHook?.Original(a1, a2, a3, a4, a5);
    }

    private unsafe IntPtr ActionTooltipDetour(AtkUnitBase* addon, void* a2, ulong a3) {
        var retVal = actionTooltipHook.Original(addon, a2, a3);
        try {
            foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                if (t is not TooltipTweaks.SubTweak st) continue;

                try {
                    st.OnActionTooltip(addon, HoveredAction);
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
        generateActionTooltipHook?.Disable();
        getItemRowHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        itemHoveredHook?.Dispose();
        actionTooltipHook?.Dispose();
        actionHoveredHook?.Dispose();
        generateItemTooltipHook?.Dispose();
        generateActionTooltipHook?.Dispose();
        getItemRowHook?.Dispose();
        foreach (var i in ItemStringPointers.Values) {
            Marshal.FreeHGlobal(i);
        }
        foreach (var i in ActionStringPointers.Values) {
            Marshal.FreeHGlobal(i);
        }
        ItemStringPointers.Clear();
        ActionStringPointers.Clear();
        base.Dispose();
    }

    public unsafe void* GenerateItemTooltipDetour(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        try {
            foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                if (t is not TooltipTweaks.SubTweak st) continue;
                try {
                    st.OnGenerateItemTooltip(numberArrayData, stringArrayData);
                } catch (Exception ex) {
                    Plugin.Error(this, t, ex);
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        return generateItemTooltipHook.Original(addonItemDetail, numberArrayData, stringArrayData);
    }

    public unsafe void* GenerateActionTooltipDetour(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        try {
            foreach (var t in SubTweaks.Where(t => t.Enabled)) {
                if (t is not TooltipTweaks.SubTweak st) continue;
                try {
                    st.OnGenerateActionTooltip(numberArrayData, stringArrayData);
                } catch (Exception ex) {
                    Plugin.Error(this, t, ex);
                }
            }
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
        return generateActionTooltipHook.Original(addonItemDetail, numberArrayData, stringArrayData);
    }

    public enum ItemTooltipField : byte {
        ItemName,
        GlamourName,
        ItemUiCategory,
        ItemDescription = 13,
        Effects = 16,
        ClassJobCategory = 22,
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

    public enum ActionTooltipField {
        ActionName,
        ActionKind,
        Unknown02,
        RangeText,
        RangeValue,
        RadiusText,
        RadiusValue,
        MPCostText,
        MPCostValue,
        RecastText,
        RecastValue,
        CastText,
        CastValue,
        Description,
        Level,
        ClassJobAbbr,
    }
}