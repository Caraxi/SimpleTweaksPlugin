using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Enums;
using SimpleTweaksPlugin.GameStructs;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Utility; 

public static unsafe class Common {

    // Common Delegates
    public delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);
    public delegate void NoReturnAddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);

    private delegate void* AddonSetupDelegate(AtkUnitBase* addon);
    private static HookWrapper<AddonSetupDelegate> addonSetupHook;

    private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);

    private delegate IntPtr GetGameAllocator();

    private static GameAlloc _gameAlloc;
    private static GetGameAllocator _getGameAllocator;

    private delegate InventoryContainer* GetInventoryContainer(IntPtr inventoryManager, InventoryType inventoryType);
    private delegate InventoryItem* GetContainerSlot(InventoryContainer* inventoryContainer, int slotId);

    private static GetInventoryContainer _getInventoryContainer;
    private static GetContainerSlot _getContainerSlot;

    public static IntPtr InventoryManagerAddress;

    public static IntPtr PlayerStaticAddress { get; private set; }

    private static IntPtr LastCommandAddress;
        
    public static Utf8String* LastCommand { get; private set; }

    public static SigScanner Scanner => Service.SigScanner;

    public static event Action FrameworkUpdate;
    
    public static void InvokeFrameworkUpdate() => FrameworkUpdate?.Invoke();
    public static void* ThrowawayOut { get; private set; } = (void*) Marshal.AllocHGlobal(1024);

    public static event Action<SetupAddonArgs> AddonSetup; 
    public static event Action<SetupAddonArgs> AddonPreSetup; 
    
    public static void Setup() {
        var gameAllocPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 49 83 CF FF 4C 8B F0");
        var getGameAllocatorPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08");

        InventoryManagerAddress = Scanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
        var getInventoryContainerPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
        var getContainerSlotPtr = Scanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");

        PlayerStaticAddress = Scanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
        LastCommandAddress = Scanner.GetStaticAddressFromSig("4C 8D 05 ?? ?? ?? ?? 41 B1 01 49 8B D4 E8 ?? ?? ?? ?? 83 EB 06");
        LastCommand = (Utf8String*) (LastCommandAddress);

        _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(gameAllocPtr);
        _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(getGameAllocatorPtr);

        _getInventoryContainer = Marshal.GetDelegateForFunctionPointer<GetInventoryContainer>(getInventoryContainerPtr);
        _getContainerSlot = Marshal.GetDelegateForFunctionPointer<GetContainerSlot>(getContainerSlotPtr);
        
        addonSetupHook = Hook<AddonSetupDelegate>("E8 ?? ?? ?? ?? 8B 83 ?? ?? ?? ?? C1 E8 14", AddonSetupDetour);
        addonSetupHook?.Enable();
    }

    private static void* AddonSetupDetour(AtkUnitBase* addon) {
        try {
            AddonPreSetup?.Invoke(new SetupAddonArgs() {
                Addon = addon
            });
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        var retVal = addonSetupHook.Original(addon);
        try {
            AddonSetup?.Invoke(new SetupAddonArgs() {
                Addon = addon
            });
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return retVal;
    }

    public static UIModule* UIModule => Framework.Instance()->GetUiModule();

    public static AtkUnitBase* GetUnitBase(string name, int index = 1) {
        return (AtkUnitBase*) Service.GameGui.GetAddonByName(name, index);
    }

    public static T* GetUnitBase<T>(string name = null, int index = 1) where T : unmanaged {
        if (string.IsNullOrEmpty(name)) {
            var attr = (Addon) typeof(T).GetCustomAttribute(typeof(Addon));
            if (attr != null) {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) return null;
            
        return (T*) Service.GameGui.GetAddonByName(name, index);
    }

    public static InventoryContainer* GetContainer(InventoryType inventoryType) {
        if (InventoryManagerAddress == IntPtr.Zero) return null;
        return _getInventoryContainer(InventoryManagerAddress, inventoryType);
    }

    public static InventoryItem* GetContainerItem(InventoryContainer* container, int slot) {
        if (container == null) return null;
        return _getContainerSlot(container, slot);
    }

    public static InventoryItem* GetInventoryItem(InventoryType inventoryType, int slotId) {
        if (InventoryManagerAddress == IntPtr.Zero) return null;
        var container = _getInventoryContainer(InventoryManagerAddress, inventoryType);
        return container == null ? null : _getContainerSlot(container, slotId);
    }

    public static IntPtr Alloc(ulong size) {
        if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
        return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
    }
        
    public static void WriteSeString(byte** startPtr, IntPtr alloc, SeString seString) {
        if (startPtr == null) return;
        var start = *(startPtr);
        if (start == null) return;
        if (start == (byte*)alloc) return;
        WriteSeString((byte*)alloc, seString);
        *startPtr = (byte*)alloc;
    }

    public static SeString ReadSeString(byte** startPtr) {
        if (startPtr == null) return null;
        var start = *(startPtr);
        if (start == null) return null;
        return ReadSeString(start);
    }

    public static SeString ReadSeString(byte* ptr) {
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
        return SeString.Parse(bytes);
    }

    public static void WriteSeString(byte* dst, SeString s) {
        var bytes = s.Encode();
        for (var i = 0; i < bytes.Length; i++) {
            *(dst + i) = bytes[i];
        }
        *(dst + bytes.Length) = 0;
    }

    public static SeString ReadSeString(Utf8String xivString) {
        var len = (int) (xivString.BufUsed > int.MaxValue ? int.MaxValue : xivString.BufUsed);
        var bytes = new byte[len];
        Marshal.Copy(new IntPtr(xivString.StringPtr), bytes, 0, len);
        return SeString.Parse(bytes);
    }

    public static void WriteSeString(Utf8String xivString, SeString s) {
        var bytes = s.Encode();
        int i;
        xivString.BufUsed = 0;
        for (i = 0; i < bytes.Length && i < xivString.BufSize - 1; i++) {
            *(xivString.StringPtr + i) = bytes[i];
            xivString.BufUsed++;
        }
        *(xivString.StringPtr + i) = 0;
    }

    public enum GameOptionKind : uint {
        GamePadMode        = 0x089, // [bool] Character Config -> Mouse Mode / GamePad Mode
        LegacyMovement     = 0x08A, // [bool] Character Config -> Control Settings -> General -> Standard Type / Legacy Type
        DisplayItemHelp    = 0x130, // [bool] Character Config -> UI Settings -> General -> Display Item Help
        DisplayActionHelp  = 0x136, // [bool] Character Config -> UI Settings -> General -> Display Action Help

        ClockDisplayType   = 0x153, // [enum/byte] 0 = Default, 1 = 24H, 2 = 12H 
        ClockTypeEorzea    = 0x155, // [bool]
        ClockTypeLocal     = 0x156, // [bool]
        ClockTypeServer    = 0x157, // [bool]
    }


    public static T GetGameOption<T>(GameOptionKind opt) {
        var optionBase = (byte**)(Service.Framework.Address.BaseAddress + 0x2B28);
        return Marshal.PtrToStructure<T>(new IntPtr(*optionBase + 0xAAE0 + (16 * (uint)opt)));
    }

    public static HookWrapper<T> Hook<T>(string signature, T detour, int addressOffset = 0) where T : Delegate {
        var addr = Scanner.ScanText(signature);
        var h = new Hook<T>(addr + addressOffset, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(void* address, T detour) where T : Delegate {
        var h = new Hook<T>(new IntPtr(address), detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(IntPtr address, NoReturnAddonOnUpdate after) {
        Hook<AddonOnUpdate> hook = null;
        hook = new Hook<AddonOnUpdate>(address, (atkUnitBase, nums, strings) => {
            var retVal = hook.Original(atkUnitBase, nums, strings);
            try {
                after(atkUnitBase, nums, strings);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
                hook.Disable();
            }
            return retVal;
        });
        var wh = new HookWrapper<AddonOnUpdate>(hook);
        return wh;
    }

    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(void* address, NoReturnAddonOnUpdate after) => HookAfterAddonUpdate(new IntPtr(address), after);
    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(string signature, NoReturnAddonOnUpdate after, int addressOffset = 0) => HookAfterAddonUpdate(Scanner.ScanText(signature) + addressOffset, after);
    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(AtkUnitBase* atkUnitBase, NoReturnAddonOnUpdate after) => HookAfterAddonUpdate(atkUnitBase->AtkEventListener.vfunc[46], after);

    public static List<IHookWrapper> HookList = new();

    public static void OpenBrowser(string url) {
        Process.Start(new ProcessStartInfo {FileName = url, UseShellExecute = true});
    }

    public static AtkValue* CreateAtkValueArray(params object[] values) {
        var atkValues = (AtkValue*) Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return null;
        try {
            for (var i = 0; i < values.Length; i++) {
                var v = values[i];
                switch (v) {
                    case uint uintValue:
                        atkValues[i].Type = ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;
                    case int intValue:
                        atkValues[i].Type = ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;
                    case float floatValue:
                        atkValues[i].Type = ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;
                    case bool boolValue:
                        atkValues[i].Type = ValueType.Bool;
                        atkValues[i].Byte = (byte) (boolValue ? 1 : 0);
                        break;
                    case string stringValue: {
                        atkValues[i].Type = ValueType.String;
                        var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                        var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                        Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                        Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                        atkValues[i].String = (byte*)stringAlloc;
                        break;
                    }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }
        } catch {
            return null;
        }

        return atkValues;
    }
    
    public static void GenerateCallback(AtkUnitBase* unitBase, params object[] values) {
        var atkValues = CreateAtkValueArray(values);
        if (atkValues == null) return;
        try {
            unitBase->FireCallback(values.Length, atkValues);
        } finally {
            for (var i = 0; i < values.Length; i++) {
                if (atkValues[i].Type == ValueType.String) {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }
            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
    }

    public static Vector4 UiColorToVector4(uint col) {
        var fa = col & 255;
        var fb = (col >> 8) & 255;
        var fg = (col >> 16) & 255;
        var fr = (col >> 24) & 255;
        return new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);
    }

    public static Vector3 UiColorToVector3(uint col) {
        var fb = (col >> 8) & 255;
        var fg = (col >> 16) & 255;
        var fr = (col >> 24) & 255;
        return new Vector3(fr / 255f, fg / 255f, fb / 255f);
    }

    public static AtkResNode* GetNodeByID(AtkUldManager uldManager, uint nodeId, NodeType? type = null) => GetNodeByID<AtkResNode>(uldManager, nodeId, type);
    public static T* GetNodeByID<T>(AtkUldManager uldManager, uint nodeId, NodeType? type = null) where T : unmanaged {
        for (var i = 0; i < uldManager.NodeListCount; i++) {
            var n = uldManager.NodeList[i];
            if (n->NodeID != nodeId || type != null && n->Type != type.Value) continue;
            return (T*)n;
        }
        return null;
    }

    public static void Shutdown() {
        if (ThrowawayOut != null) {
            Marshal.FreeHGlobal(new IntPtr(ThrowawayOut));
            ThrowawayOut = null;
        }
        
        addonSetupHook?.Disable();
        addonSetupHook?.Dispose();
    }

    public const int UnitListCount = 18;
    public static AtkUnitBase* GetAddonByID(uint id) {
        var unitManagers = &AtkStage.GetSingleton()->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        for (var i = 0; i < UnitListCount; i++) {
            var unitManager = &unitManagers[i];
            var unitBaseArray = &(unitManager->AtkUnitEntries);
            for (var j = 0; j < unitManager->Count; j++) {
                var unitBase = unitBaseArray[j];
                if (unitBase->ID == id) {
                    return unitBase;
                }
            }
        }

        return null;
    }

    public static string ValueString(this AtkValue v) {
        return v.Type switch {
            ValueType.Int => $"{v.Int}",
            ValueType.String => Marshal.PtrToStringUTF8(new IntPtr(v.String)),
            ValueType.UInt => $"{v.UInt}",
            ValueType.Bool => $"{v.Byte != 0}",
            ValueType.Float => $"{v.Float}",
            ValueType.Vector => "[Vector]",
            ValueType.AllocatedString => "[Allocated String]",
            ValueType.AllocatedVector => "[Allocated Vector]",
            _ => $"Unknown Type: {v.Type}"
        };
    }
    
}

public unsafe class SetupAddonArgs {
    public AtkUnitBase* Addon { get; init; }
    private string addonName;
    public string AddonName => addonName ??= MemoryHelper.ReadString(new IntPtr(Addon->Name), 0x20).Split('\0')[0];
}
