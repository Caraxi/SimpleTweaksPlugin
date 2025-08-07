using System;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

public unsafe class TooltipTweaks : SubTweakManager<TooltipTweaks.SubTweak> {
    public override bool AlwaysEnabled => true;

    [TweakCategory(TweakCategory.Tooltip)]
    public abstract class SubTweak : BaseTweak {
        public override string Key => $"{nameof(TooltipTweaks)}@{base.Key}";
        public virtual void OnActionTooltip(AtkUnitBase* addonActionDetail, HoveredActionDetail action) { }
        public virtual void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) { }
        public virtual void OnGenerateActionTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) { }

        protected static SeString GetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field) => GetTooltipString(stringArrayData, (int)field);
        protected static SeString GetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ActionTooltipField field) => GetTooltipString(stringArrayData, (int)field);

        protected static SeString GetTooltipString(StringArrayData* stringArrayData, int field) {
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

        protected static void SetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ItemTooltipField field, SeString seString) {
            seString ??= new SeString();
            var bytes = seString.EncodeWithNullTerminator();
            stringArrayData->SetValue((int)field, bytes, false);
        }

        protected static void SetTooltipString(StringArrayData* stringArrayData, TooltipTweaks.ActionTooltipField field, SeString seString) {
            seString ??= new SeString();
            var bytes = seString.EncodeWithNullTerminator();
            stringArrayData->SetValue((int)field, bytes, false);
        }

        protected static InventoryItem Item => HoveredItem;
        protected static HoveredActionDetail Action => HoveredAction;
        protected static uint LoadedItem => LastLoadedItem;
    }

    private delegate IntPtr ActionTooltipDelegate(AtkUnitBase* a1, void* a2, ulong a3);
    
    private HookWrapper<ActionTooltipDelegate> actionTooltipHook;

    private delegate byte ItemHoveredDelegate(IntPtr a1, IntPtr* a2, int* containerId, ushort* slotId, IntPtr a5, uint slotIdInt, IntPtr a7);

    private HookWrapper<ItemHoveredDelegate> itemHoveredHook;
    
    [TweakHook(typeof(AgentActionDetail), nameof(AgentActionDetail.HandleActionHover), nameof(ActionHoveredDetour))]
    private HookWrapper<AgentActionDetail.Delegates.HandleActionHover> actionHoveredHook;

    private delegate void* GenerateItemTooltip(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);

    private HookWrapper<GenerateItemTooltip> generateItemTooltipHook;

    private delegate void* GenerateActionTooltip(AtkUnitBase* addonActionDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);

    private HookWrapper<GenerateActionTooltip> generateActionTooltipHook;

    private delegate void* GetItemRowDelegate(uint itemId);

    private HookWrapper<GetItemRowDelegate> getItemRowHook;

    protected override void Setup() {
        AddChangelog("1.8.5.1", "Added additional protections to attempt to reduce crashing. Please report any crashes you believe may be related to tooltips.");
        AddChangelog("1.8.6.1", "Yet another attempt at fixing crashes.");
        base.Setup();
    }

    protected override void Enable() {
        if (!Ready) return;
        
        itemHoveredHook ??= Common.Hook<ItemHoveredDelegate>("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 4C 89 A4 24", ItemHoveredDetour);
        actionTooltipHook ??= Common.Hook<ActionTooltipDelegate>("48 89 5C 24 ?? 55 56 57 41 54 41 56 48 83 EC 30 48 8B 9A", ActionTooltipDetour);
        generateItemTooltipHook ??= Common.Hook<GenerateItemTooltip>("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA", GenerateItemTooltipDetour);
        generateActionTooltipHook ??= Common.Hook<GenerateActionTooltip>("E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF", GenerateActionTooltipDetour);
        getItemRowHook ??= Common.Hook<GetItemRowDelegate>("E8 ?? ?? ?? ?? 4C 8B F8 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B D0 48 8D 0D", GetItemRowDetour);

        itemHoveredHook?.Enable();
        actionTooltipHook?.Enable();
        generateItemTooltipHook?.Enable();
        generateActionTooltipHook?.Enable();
        getItemRowHook?.Enable();
        
        base.Enable();
    }

    private void* GetItemRowDetour(uint itemId) {
        LastLoadedItem = itemId;
        return getItemRowHook.Original(itemId);
    }

    public class HoveredActionDetail {
        public ActionKind Category;
        public uint Id;
        public int Flag;
        public bool IsLovmActionDetail;
    }

    public static readonly HoveredActionDetail HoveredAction = new HoveredActionDetail();

    public static uint LastLoadedItem { get; private set; }

    private void ActionHoveredDetour(AgentActionDetail* agent, ActionKind actionKind, uint actionId, int flag, bool isLovmActionDetail) {
        HoveredAction.Category = actionKind;
        HoveredAction.Id = actionId;
        HoveredAction.Flag = flag;
        HoveredAction.IsLovmActionDetail = isLovmActionDetail;
        actionHoveredHook?.Original(agent, actionKind, actionId, flag, isLovmActionDetail);
    }

    private IntPtr ActionTooltipDetour(AtkUnitBase* addon, void* a2, ulong a3) {
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

    private byte ItemHoveredDetour(IntPtr a1, IntPtr* a2, int* containerid, ushort* slotid, IntPtr a5, uint slotidint, IntPtr a7) {
        var returnValue = itemHoveredHook.Original(a1, a2, containerid, slotid, a5, slotidint, a7);
        HoveredItem = *(InventoryItem*)(a7);
        return returnValue;
    }

    protected override void Disable() {
        itemHoveredHook?.Disable();
        actionTooltipHook?.Disable();
        generateItemTooltipHook?.Disable();
        generateActionTooltipHook?.Disable();
        getItemRowHook?.Disable();
    }

    public override void Dispose() {
        itemHoveredHook?.Dispose();
        actionTooltipHook?.Dispose();
        generateItemTooltipHook?.Dispose();
        generateActionTooltipHook?.Dispose();
        getItemRowHook?.Dispose();
        base.Dispose();
    }

    public void* GenerateItemTooltipDetour(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
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

    public void* GenerateActionTooltipDetour(AtkUnitBase* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
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
