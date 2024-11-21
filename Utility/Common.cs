using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Networking.Http;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Debugging;
using Action = System.Action;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Utility;

public unsafe class Common {
    [PluginService] private static IGameInteropProvider ImNotGonnaCallItThat { get; set; } = null!;

    private static IntPtr LastCommandAddress;

    public static Utf8String* LastCommand { get; private set; }

    public static uint ClientStructsVersion => CsVersion.Value;
    private static readonly Lazy<uint> CsVersion = new(() => (uint?)typeof(FFXIVClientStructs.ThisAssembly).Assembly.GetName().Version?.Build ?? 0U);

    public static event Action FrameworkUpdate;

    public static void InvokeFrameworkUpdate() {
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

    public static void* ThrowawayOut { get; private set; } = (void*)Marshal.AllocHGlobal(1024);

    public static void Setup() {
        LastCommandAddress = Service.SigScanner.GetStaticAddressFromSig("4C 8D 05 ?? ?? ?? ?? 41 B1 01 49 8B D4 E8 ?? ?? ?? ?? 83 EB 06");
        LastCommand = (Utf8String*)(LastCommandAddress);

        updateCursorHook = Hook<AtkModuleUpdateCursor>("48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 4C 8B F1 E8 ?? ?? ?? ?? 49 8B CE", UpdateCursorDetour);
        updateCursorHook?.Enable();
    }

    public static UIModule* UIModule => Framework.Instance()->GetUIModule();

    public static bool GetUnitBase(string name, out AtkUnitBase* unitBase, int index = 1) {
        unitBase = GetUnitBase(name, index);
        return unitBase != null;
    }

    public static AtkUnitBase* GetUnitBase(string name, int index = 1) {
        return (AtkUnitBase*)Service.GameGui.GetAddonByName(name, index);
    }

    public static T* GetUnitBase<T>(string name = null, int index = 1) where T : unmanaged {
        if (string.IsNullOrEmpty(name)) {
            var attr = (AddonAttribute) typeof(T).GetCustomAttribute(typeof(AddonAttribute));
            if (attr != null) {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) return null;

        return (T*)Service.GameGui.GetAddonByName(name, index);
    }

    public static bool GetUnitBase<T>(out T* unitBase, string name = null, int index = 1) where T : unmanaged {
        unitBase = null;
        if (string.IsNullOrEmpty(name)) {
            var attr = (AddonAttribute) typeof(T).GetCustomAttribute(typeof(AddonAttribute));
            if (attr != null) {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) return false;

        unitBase = (T*)Service.GameGui.GetAddonByName(name, index);
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

    public static SeString ReadSeString(Utf8String xivString) => SeString.Parse(xivString);

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
        var h = ImNotGonnaCallItThat.HookFromAddress(addr + addressOffset, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(void* address, T detour) where T : Delegate {
        var h = ImNotGonnaCallItThat.HookFromAddress(new nint(address), detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(nuint address, T detour) where T : Delegate {
        var h = ImNotGonnaCallItThat.HookFromAddress((nint)address, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static HookWrapper<T> Hook<T>(nint address, T detour) where T : Delegate {
        var h = ImNotGonnaCallItThat.HookFromAddress(address, detour);
        var wh = new HookWrapper<T>(h);
        HookList.Add(wh);
        return wh;
    }

    public static List<IHookWrapper> HookList = new();

    public static void OpenBrowser(string url) {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    public static AtkValue* CreateAtkValueArray(params object[] values) {
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
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
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
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
            unitBase->FireCallback((uint)values.Length, atkValues);
        } finally {
            for (var i = 0; i < values.Length; i++) {
                if (atkValues[i].Type == ValueType.String) {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }

            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
    }

    public static AtkValue* SendEvent(AgentId agentId, ulong eventKind, params object[] eventparams) {
        var agent = AgentModule.Instance()->GetAgentByInternalId(agentId);
        return agent == null ? null : SendEvent(agent, eventKind, eventparams);
    }

    public static AtkValue* SendEvent(AgentInterface* agentInterface, ulong eventKind, params object[] eventParams) {
        var eventObject = stackalloc AtkValue[1];
        return SendEvent(agentInterface, eventObject, eventKind, eventParams);
    }

    public static AtkValue* SendEvent(AgentInterface* agentInterface, AtkValue* eventObject, ulong eventKind, params object[] eventParams) {
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

    public static AtkResNode* GetNodeByID(AtkUnitBase* unitBase, uint nodeId, NodeType? type = null) => GetNodeByID(&unitBase->UldManager, nodeId, type);
    public static AtkResNode* GetNodeByID(AtkComponentBase* component, uint nodeId, NodeType? type = null) => GetNodeByID(&component->UldManager, nodeId, type);
    public static AtkResNode* GetNodeByID(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) => GetNodeByID<AtkResNode>(uldManager, nodeId, type);

    public static T* GetNodeByID<T>(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) where T : unmanaged {
        if (uldManager == null) return null;
        if (uldManager->NodeList == null) return null;
        for (var i = 0; i < uldManager->NodeListCount; i++) {
            var n = uldManager->NodeList[i];
            if (n == null || n->NodeId != nodeId || type != null && n->Type != type.Value) continue;
            return (T*)n;
        }

        return null;
    }

    public static bool GetNodeById(AtkUldManager* uldManager, uint nodeId, out AtkResNode* node) {
        node = GetNodeByID<AtkResNode>(uldManager, nodeId, NodeType.Res);
        return node != null;
    }

    public static bool GetNodeById(AtkUldManager* uldManager, uint nodeId, out AtkTextNode* node) {
        node = GetNodeByID<AtkTextNode>(uldManager, nodeId, NodeType.Text);
        return node != null;
    }

    public static bool GetNodeById(AtkUldManager* uldManager, uint nodeId, out AtkImageNode* node) {
        node = GetNodeByID<AtkImageNode>(uldManager, nodeId, NodeType.Image);
        return node != null;
    }

    public static AtkResNode* GetNodeByIDChain(AtkResNode* node, params int[] ids)
    {
        if (node == null || ids.Length <= 0)
            return null;

        if (node->NodeId == ids[0]) {
            if (ids.Length == 1)
                return node;

            var newList = new List<int>(ids);
            newList.RemoveAt(0);

            var childNode = node->ChildNode;
            if (childNode != null)
                return GetNodeByIDChain(childNode, [.. newList]);

            if ((int)node->Type >= 1000) {
                var componentNode = node->GetAsAtkComponentNode();
                var component = componentNode->Component;
                var uldManager = component->UldManager;
                childNode = uldManager.NodeList[0];
                return childNode == null ? null : GetNodeByIDChain(childNode, [.. newList]);
            }

            return null;
        }

        var sibNode = node->PrevSiblingNode;
        return sibNode != null ? GetNodeByIDChain(sibNode, ids) : null;
    }

    public static void Shutdown() {
        if (ThrowawayOut != null) {
            Marshal.FreeHGlobal(new IntPtr(ThrowawayOut));
            ThrowawayOut = null;
        }

        updateCursorHook?.Disable();
        updateCursorHook?.Dispose();
        httpClient?.Dispose();
    }

    public const int UnitListCount = 18;

    public static AtkUnitBase* GetAddonByID(uint id) {
        var unitManagers = &AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        for (var i = 0; i < UnitListCount; i++) {
            var unitManager = &unitManagers[i];
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->Entries.Length))) {
                var unitBase = unitManager->Entries[j].Value;
                if (unitBase != null && unitBase->Id == id) {
                    return unitBase;
                }
            }
        }

        return null;
    }

    public static void CloseAddon(string name, bool unk = true, bool callHideCallback = true, uint setShowHideFlags = 0) {
        var addon = GetUnitBase(name);
        if (addon != null) addon->Hide(unk, callHideCallback, setShowHideFlags);
    }

    public static AgentInterface* GetAgent(AgentId agentId) {
        return Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(agentId);
    }

    public static T* GetAgent<T>() where T : unmanaged {
        var attr = typeof(T).GetCustomAttribute<AgentAttribute>();
        if (attr == null) return null;
        return (T*)GetAgent(attr.Id);
    }

    private delegate void* AtkModuleUpdateCursor(RaptureAtkModule* module);

    private static HookWrapper<AtkModuleUpdateCursor> updateCursorHook;

    private static AtkCursor.CursorType _lockedCursorType = AtkCursor.CursorType.Arrow;

    private static void* UpdateCursorDetour(RaptureAtkModule* module) {
        if (_lockedCursorType != AtkCursor.CursorType.Arrow) {
            var cursor = AtkStage.Instance()->AtkCursor;
            if (cursor.Type != _lockedCursorType) {
                AtkStage.Instance()->AtkCursor.SetCursorType(_lockedCursorType, 1);
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
        AtkStage.Instance()->AtkCursor.SetCursorType(cursorType);
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

    private static HttpClient httpClient;
    private static HappyEyeballsCallback happyEyeballsCallback;

    public static HttpClient HttpClient {
        get {
            if (httpClient != null) return httpClient;
            happyEyeballsCallback = new HappyEyeballsCallback();
            httpClient = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All, ConnectCallback = happyEyeballsCallback.ConnectCallback, });

            return httpClient;
        }
    }

    public static Extensions.PointerReadOnlySpanUnboxer<AtkResNode> GetNodeList(AtkUldManager* uldManager) => new ReadOnlySpan<Pointer<AtkResNode>>(uldManager->NodeList, uldManager->NodeListCount).Unbox();
    public static Extensions.PointerReadOnlySpanUnboxer<AtkResNode> GetNodeList(AtkUnitBase* unitBase) => GetNodeList(&unitBase->UldManager);
    public static Extensions.PointerReadOnlySpanUnboxer<AtkResNode> GetNodeList(AtkComponentBase* component) => GetNodeList(&component->UldManager);

    public static string DefaultStringIfEmptyOrWhitespace(string str, string defaultString = "") => string.IsNullOrWhiteSpace(str) ? defaultString : str;
}
