using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Debugging;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Utility; 

public static unsafe class Common {

    // Common Delegates
    public delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);
    public delegate void NoReturnAddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);

    private delegate void* AddonSetupDelegate(AtkUnitBase* addon);
    private static HookWrapper<AddonSetupDelegate> addonSetupHook;
    
    private delegate void FinalizeAddonDelegate(AtkUnitManager* unitManager, AtkUnitBase** atkUnitBase);
    private static HookWrapper<FinalizeAddonDelegate> finalizeAddonHook;

    private static IntPtr LastCommandAddress;
        
    public static Utf8String* LastCommand { get; private set; }
    
    public static event Action FrameworkUpdate;
    
    public static void InvokeFrameworkUpdate() {
        if (!SimpleTweaksPlugin.Plugin.PluginConfig.NoFools && Fools.IsFoolsDay) Fools.FrameworkUpdate();
        if (!PerformanceMonitor.DoFrameworkMonitor) {
            FrameworkUpdate?.Invoke();
            return;
        }

        if (FrameworkUpdate == null) return;
        foreach (var updateDelegate in FrameworkUpdate.GetInvocationList()) {
            PerformanceMonitor.Begin($"[FrameworkUpdate]{updateDelegate.Target?.GetType().Name}.{updateDelegate.Method.Name}");
            updateDelegate.DynamicInvoke();
            PerformanceMonitor.End($"[FrameworkUpdate]{updateDelegate.Target?.GetType().Name}.{updateDelegate.Method.Name}");
        }
    }
    public static void* ThrowawayOut { get; private set; } = (void*) Marshal.AllocHGlobal(1024);

    public static event Action<SetupAddonArgs> AddonSetup; 
    public static event Action<SetupAddonArgs> AddonPreSetup; 
    public static event Action<SetupAddonArgs> AddonFinalize;
    
    public static void Setup() {
        LastCommandAddress = Service.SigScanner.GetStaticAddressFromSig("4C 8D 05 ?? ?? ?? ?? 41 B1 01 49 8B D4 E8 ?? ?? ?? ?? 83 EB 06");
        LastCommand = (Utf8String*) (LastCommandAddress);
        
        addonSetupHook = Hook<AddonSetupDelegate>("E8 ?? ?? ?? ?? 8B 83 ?? ?? ?? ?? C1 E8 14", AddonSetupDetour);
        addonSetupHook?.Enable();

        finalizeAddonHook = Hook<FinalizeAddonDelegate>("E8 ?? ?? ?? ?? 48 8B 7C 24 ?? 41 8B C6", FinalizeAddonDetour);
        finalizeAddonHook?.Enable();

        updateCursorHook = Hook<AtkModuleUpdateCursor>("48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 4C 8B F1 E8 ?? ?? ?? ?? 49 8B CE", UpdateCursorDetour);
        updateCursorHook?.Enable();
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

    private static void FinalizeAddonDetour(AtkUnitManager* unitManager, AtkUnitBase** atkUnitBase) {
        try {
            AddonFinalize?.Invoke(new SetupAddonArgs() {
                Addon = atkUnitBase[0]
            });
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
        finalizeAddonHook?.Original(unitManager, atkUnitBase);
    }

    public static UIModule* UIModule => Framework.Instance()->GetUiModule();


    public static bool GetUnitBase(string name, out AtkUnitBase* unitBase, int index = 1) {
        unitBase = GetUnitBase(name, index);
        return unitBase != null;
    }
    
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

    public static bool GetUnitBase<T>(out T* unitBase, string name=null, int index = 1) where T : unmanaged {
        unitBase = null;
        if (string.IsNullOrEmpty(name)) {
            var attr = (Addon) typeof(T).GetCustomAttribute(typeof(Addon));
            if (attr != null) {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) return false;
            
        unitBase = (T*) Service.GameGui.GetAddonByName(name, index);
        return unitBase != null;
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

    public static HookWrapper<T> Hook<T>(string signature, T detour, int addressOffset = 0) where T : Delegate {
        var addr = Service.SigScanner.ScanText(signature);
        var h = Dalamud.Hooking.Hook<T>.FromAddress(addr + addressOffset, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(void* address, T detour) where T : Delegate {
        var h =  Dalamud.Hooking.Hook<T>.FromAddress(new nint(address), detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(nuint address, T detour) where T : Delegate {
        var h = Dalamud.Hooking.Hook<T>.FromAddress((nint)address, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(nint address, T detour) where T : Delegate {
        var h = Dalamud.Hooking.Hook<T>.FromAddress(address, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(IntPtr address, NoReturnAddonOnUpdate after) {
        Hook<AddonOnUpdate> hook = null;
        hook = Dalamud.Hooking.Hook<AddonOnUpdate>.FromAddress(address, (atkUnitBase, nums, strings) => {
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
    public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(string signature, NoReturnAddonOnUpdate after, int addressOffset = 0) => HookAfterAddonUpdate(Service.SigScanner.ScanText(signature) + addressOffset, after);
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EventObject {
        [FieldOffset(0)] public ulong Unknown0;
        [FieldOffset(8)] public ulong Unknown8;
    }

    public static EventObject* SendEvent(AgentInterface* agentInterface, ulong eventKind, params object[] eventParams) {
        var eventObject = stackalloc EventObject[1];
        return SendEvent(agentInterface, eventObject, eventKind, eventParams);
    }
    
    public static EventObject* SendEvent(AgentInterface* agentInterface, EventObject* eventObject, ulong eventKind, params object[] eventParams) {
        var atkValues = CreateAtkValueArray(eventParams);
        if (atkValues == null) return eventObject;
        try {
            agentInterface->ReceiveEvent(eventObject, atkValues, (uint)eventParams.Length, eventKind);
            return eventObject;
        } finally {
            for (var i = 0; i < eventParams.Length; i++) {
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

    public static AtkResNode* GetNodeByID(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) => GetNodeByID<AtkResNode>(uldManager, nodeId, type);
    public static T* GetNodeByID<T>(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) where T : unmanaged {
        for (var i = 0; i < uldManager->NodeListCount; i++) {
            var n = uldManager->NodeList[i];
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
        
        updateCursorHook?.Disable();
        updateCursorHook?.Dispose();
        
        finalizeAddonHook?.Disable();
        finalizeAddonHook?.Dispose();
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
            ValueType.AllocatedString => Marshal.PtrToStringUTF8(new IntPtr(v.String))?.TrimEnd('\0') ?? string.Empty,
            ValueType.AllocatedVector => "[Allocated Vector]",
            _ => $"Unknown Type: {v.Type}"
        };
    }

    public static void CloseAddon(string name, bool unk = true) {
        var addon = GetUnitBase(name);
        if (addon != null) addon->Hide(unk);
    }

    public static AgentInterface* GetAgent(AgentId agentId) {
        return Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(agentId);
    }

    public static T* GetAgent<T>() where T : unmanaged {
        var attr = typeof(T).GetCustomAttribute<AgentAttribute>();
        if (attr == null) return null;
        return (T*)GetAgent(attr.ID);
    }


    private delegate void* AtkModuleUpdateCursor(RaptureAtkModule* module);
    private static HookWrapper<AtkModuleUpdateCursor> updateCursorHook;

    private static AtkCursor.CursorType _lockedCursorType = AtkCursor.CursorType.Arrow;
    
    private static void* UpdateCursorDetour(RaptureAtkModule* module) {
        if (_lockedCursorType != AtkCursor.CursorType.Arrow) {
            var cursor = AtkStage.GetSingleton()->AtkCursor;
            if (cursor.Type != _lockedCursorType) {
                AtkStage.GetSingleton()->AtkCursor.SetCursorType(_lockedCursorType, 1);
            }
            return null;
        }

        return updateCursorHook.Original(module);
    }

    public static void ForceMouseCursor(AtkCursor.CursorType cursorType) {
        if (cursorType == AtkCursor.CursorType.Arrow) {
            UnforceMouseCursor();
            return;
        }
        _lockedCursorType = cursorType;
        AtkStage.GetSingleton()->AtkCursor.SetCursorType(cursorType);
        updateCursorHook?.Enable();
    }

    public static void UnforceMouseCursor() {
        _lockedCursorType = AtkCursor.CursorType.Arrow;
        updateCursorHook?.Disable();
    }
    
    public static string GetTexturePath(AtkImageNode* imageNode) {
        if (imageNode == null) return null;
        var partList = imageNode->PartsList;
        if (partList == null || partList->Parts == null) return null;
        if (imageNode->PartId >= partList->PartCount) return null;
        var part = &partList->Parts[imageNode->PartId];
        var textureInfo = part->UldAsset;
        if (textureInfo == null) return null;
        if (textureInfo->AtkTexture.TextureType != TextureType.Resource) return null;
        var resource = textureInfo->AtkTexture.Resource;
        if (resource == null) return null;
        var handle = resource->TexFileResourceHandle;
        if (handle == null) return null;
        return handle->ResourceHandle.FileName.ToString();
    }
    
    public static string ReadString(byte* b, int maxLength = 0, bool nullIsEmpty = true) {
        if (b == null) return nullIsEmpty ? string.Empty : null;
        if (maxLength > 0) return Encoding.UTF8.GetString(b, maxLength).Split('\0')[0];
        var l = 0;
        while (b[l] != 0) l++;
        return Encoding.UTF8.GetString(b, l);
    }
}

public unsafe class SetupAddonArgs {
    public AtkUnitBase* Addon { get; init; }
    private string addonName;
    public string AddonName => addonName ??= MemoryHelper.ReadString(new IntPtr(Addon->Name), 0x20).Split('\0')[0];
}
