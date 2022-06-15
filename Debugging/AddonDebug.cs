using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class AddonDebug : DebugHelper {
    public override string Name => "Addon Logging";

    private delegate void* FireCallbackDelegate(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues, ulong a4);
    private HookWrapper<FireCallbackDelegate> fireCallbackHook;
    private bool enabled = false;

    public delegate void* OnSetupDelegate(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues);
    private static Dictionary<string, SetupHook> setupHooks = new();

    private delegate void* ListHandlerSetupDelegate(void* a1, AtkUnitBase* a2, void* a3);
    private HookWrapper<ListHandlerSetupDelegate> listHandlerSetupHook;

    private delegate byte* FormatAddonTextDelegate(RaptureTextModule* raptureTextModule, uint addonTextId, int a3, void* a4, void** a5, void** a6);
    private HookWrapper<FormatAddonTextDelegate> formatAddonTextHook;

    public static bool IsSetupHooked(AtkUnitBase* atkUnitBase) {
        var name = Encoding.UTF8.GetString(atkUnitBase->Name, 0x20).TrimEnd();
        return setupHooks.ContainsKey(name);
    }

    public class Callback {
        public string AtkUnitBaseName = string.Empty;
        public List<object> AtkValues = new();
        public List<ValueType> AtkValueTypes = new();
        public void* ReturnValue;
        public AtkUnitBase* AtkUnitBase;
        public ulong A4Value;
    }

    public class SetupCall {
        public string AtkUnitBaseName = string.Empty;
        public List<object> AtkValues = new();
        public List<ValueType> AtkValueTypes = new();
        public void* ReturnValue;
        public AtkUnitBase* AtkUnitBase;
    }


    public unsafe class SetupHook{
        public HookWrapper<OnSetupDelegate> Hook;
        public List<SetupCall> Calls = new();

        public SetupHook(AtkUnitBase* unitBase) {
            Hook = Common.Hook<OnSetupDelegate>(unitBase->AtkEventListener.vfunc[45], SetupDetour);
        }

        public void* SetupDetour(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues) {
            var atkValueList = new List<object>();
            var atkValueTypeList = new List<ValueType>();
            try {
                var a = atkValues;
                for (var i = 0; i < valueCount; i++) {
                    atkValueTypeList.Add(a->Type);
                    switch (a->Type) {
                        case ValueType.Int: {
                            atkValueList.Add(a->Int);
                            break;
                        }
                        case ValueType.String8:
                        case ValueType.String: {
                            atkValueList.Add(Marshal.PtrToStringUTF8(new IntPtr(a->String)));
                            break;
                        }
                        case ValueType.UInt: {
                            atkValueList.Add(a->UInt);
                            break;
                        }
                        case ValueType.Bool: {
                            atkValueList.Add(a->Byte != 0);
                            break;
                        }
                        default: {
                            atkValueList.Add($"Unknown Type: {a->Type}");
                            break;
                        }
                    }
                    a++;
                }
            } catch {
                return Hook.Original(atkUnitBase, valueCount, atkValues);
            }
            var ret = Hook.Original(atkUnitBase, valueCount, atkValues);
            try {
                Calls.Insert(0, new SetupCall() {
                    AtkUnitBaseName = Encoding.UTF8.GetString(atkUnitBase->Name, 0x20).TrimEnd(),
                    AtkUnitBase = atkUnitBase,
                    ReturnValue = ret,
                    AtkValues = atkValueList,
                    AtkValueTypes = atkValueTypeList,
                });
            } catch {
                //
            }

            return ret;
        }

    }


    public List<Callback> Callbacks = new();

    public override void Draw() {


        if (ImGui.BeginTabBar("addonCallLoggingTabs")) {

            if (ImGui.BeginTabItem("Callbacks")) {

                if (ImGui.Checkbox($"Enable Logging", ref enabled)) {
                    fireCallbackHook?.Disable();
                    if (enabled) {
                        fireCallbackHook ??= Common.Hook<FireCallbackDelegate>("E8 ?? ?? ?? ?? 8B 4C 24 20 0F B6 D8", CallbackDetour);
                        fireCallbackHook?.Enable();
                    }
                }


                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                ImGui.InputText("##searchCallbacks", ref callbacksSearch, 50);

                ImGui.SameLine();
                if (ImGui.Button("Clear")) {
                    Callbacks.Clear();
                }
                ImGui.Separator();

                if (ImGui.BeginTable("callbacksTable", 4, ImGuiTableFlags.RowBg)) {
                    ImGui.TableSetupColumn("Addon", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Values", ImGuiTableColumnFlags.WidthFixed, 300);
                    ImGui.TableSetupColumn("A4", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Return", ImGuiTableColumnFlags.WidthFixed, 60);

                    ImGui.TableHeadersRow();

                    foreach (var cb in Callbacks.Where(cb => callbacksSearch.Length < 0 || cb.AtkUnitBaseName.Contains(callbacksSearch))) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{cb.AtkUnitBaseName}");
                        ImGui.TableNextColumn();
                        for (var i = 0; i < cb.AtkValues.Count; i++) {
                            ImGui.Text($"{i} [{cb.AtkValueTypes[i]}]:   {cb.AtkValues[i]}");
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text($"{cb.A4Value}");
                        DebugManager.ClickToCopyText($"{cb.A4Value:X}");
                        ImGui.TableNextColumn();
                        DebugManager.ClickToCopyText($"{(ulong)cb.ReturnValue:X}");
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Setups")) {
                if (setupHooks.Count > 0) {
                    if (ImGui.BeginTabBar("addonCallSetups")) {
                        foreach (var (name, hook) in setupHooks) {
                            if (hook.Hook.IsDisposed) continue;
                            if (ImGui.BeginTabItem($"{name}##setupHookTab")) {
                                var e = hook.Hook.IsEnabled;

                                if (ImGui.Checkbox($"Enable Logging##setupHookEnabledCheckbox{name}", ref e)) {
                                    if (e) {
                                        hook.Hook.Enable();
                                    } else {
                                        hook.Hook.Disable();
                                    }
                                }

                                ImGui.SameLine();
                                if (ImGui.Button($"Remove Hook##setupHookRemoveButton{name}")) {
                                    hook.Hook.Dispose();
                                    hook.Calls.Clear();
                                }

                                ImGui.Separator();

                                if (ImGui.BeginTable("callbacksTable", 3, ImGuiTableFlags.RowBg)) {
                                    ImGui.TableSetupColumn("Addon", ImGuiTableColumnFlags.WidthFixed, 150);
                                    ImGui.TableSetupColumn("Values", ImGuiTableColumnFlags.WidthFixed, 300);
                                    ImGui.TableSetupColumn("Return", ImGuiTableColumnFlags.WidthFixed, 90);

                                    ImGui.TableHeadersRow();

                                    foreach (var cb in hook.Calls) {
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{cb.AtkUnitBaseName}");
                                        ImGui.TableNextColumn();
                                        for (var i = 0; i < cb.AtkValues.Count; i++) {
                                            ImGui.Text($"{i} [{cb.AtkValueTypes[i]}]:   {cb.AtkValues[i]}");
                                        }

                                        ImGui.TableNextColumn();
                                        DebugManager.ClickToCopyText($"{(ulong)cb.ReturnValue:X}");
                                    }

                                    ImGui.EndTable();
                                }


                                ImGui.EndTabItem();
                            }
                        }



                        ImGui.EndTabBar();
                    }

                } else {
                    ImGui.Text("No hooks are enabled.");
                }





                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("List Handlers")) {

                listHandlerSetupHook ??= Common.Hook("E8 ?? ?? ?? ?? 41 B1 1E", new ListHandlerSetupDelegate(ListHandlerSetupDetour));

                var e = listHandlerSetupHook.IsEnabled;
                if (ImGui.Checkbox("Log List Handler Setups", ref e)) {
                    if (e)
                        listHandlerSetupHook?.Enable();
                    else
                        listHandlerSetupHook?.Disable();
                }
                ImGui.SameLine();
                if (ImGui.Button("Clear##clearListHandlers")) {
                    listHandlerSetupCalls.Clear();
                }

                ImGui.Separator();

                if (ImGui.BeginTable("ListHandlerSetups", 3)) {
                    ImGui.TableSetupColumn("this");
                    ImGui.TableSetupColumn("Addon");
                    ImGui.TableSetupColumn("Callback");

                    ImGui.TableHeadersRow();

                    foreach (var l in listHandlerSetupCalls) {
                        ImGui.TableNextColumn();
                        DebugManager.ClickToCopy(l.This);
                        ImGui.TableNextColumn();
                        ImGui.Text($"{l.AtkUnitBaseName}");
                        ImGui.TableNextColumn();
                        DebugManager.ClickToCopy(l.UpdateItemCallback);

                        var addr = (ulong) l.UpdateItemCallback;
                        if (addr > 0) {
                            var baseAddr = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress;
                            var offset = addr - baseAddr;
                            ImGui.SameLine();
                            DebugManager.ClickToCopyText($"ffxiv_dx11.exe+{offset:X}");
                        }
                    }
                    ImGui.EndTable();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Addon Text")) {

                var addonTextLoggingEnabled = formatAddonTextHook?.IsEnabled ?? false;

                if (ImGui.Checkbox("Enable Logging##addonTextLoggingToggle", ref addonTextLoggingEnabled)) {
                    if (addonTextLoggingEnabled) {
                        formatAddonTextHook ??= Common.Hook<FormatAddonTextDelegate>("E8 ?? ?? ?? ?? 48 8D 4F 40 48 8B D0", FormatAddonTextDetour);
                        formatAddonTextHook?.Enable();
                    } else {
                        formatAddonTextHook?.Disable();
                    }
                }

                ImGui.Separator();

            }

            ImGui.EndTabBar();
        }
    }

    private class CaughtAddonTextFormatting {
        private uint AddonTextId;
        private SeString FormattedString;
    }

    private byte* FormatAddonTextDetour(RaptureTextModule* rapturetextmodule, uint addontextid, int a3, void* a4, void** a5, void** a6) {
        var retVal = formatAddonTextHook.Original(rapturetextmodule, addontextid, a3, a4, a5, a6);

        try {
            if (retVal != null) {
                var str = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(retVal));
                SimpleLog.Log($"Format Addon Text: {addontextid} -> {str.TextValue}");
            } else {
                SimpleLog.Log($"Format Addon Text: {addontextid} -> Returning NULL");
            }



        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }




        return retVal;
    }

    public class ListHandlerSetupCall {
        public string AtkUnitBaseName;
        public void* UpdateItemCallback;
        public void* This;
    }

    private List<ListHandlerSetupCall> listHandlerSetupCalls = new();


    private void* ListHandlerSetupDetour(void* a1, AtkUnitBase* atkUnitBase, void* updateItemCallback) {
        var retVal = listHandlerSetupHook.Original(a1, atkUnitBase, updateItemCallback);
        try {
            listHandlerSetupCalls.Insert(0, new ListHandlerSetupCall() {
                This = a1,
                UpdateItemCallback = updateItemCallback,
                AtkUnitBaseName = Encoding.UTF8.GetString(atkUnitBase->Name, 0x20).Trim()
            });
        } catch {
            //
        }
        return retVal;
    }

    public static void HookOnSetup(AtkUnitBase* atkUnitBase) {
        var name = Encoding.UTF8.GetString(atkUnitBase->Name, 0x20).TrimEnd();
        setupHooks.Add(name, new SetupHook(atkUnitBase));
    }

    private string callbacksSearch = string.Empty;

    private void* CallbackDetour(AtkUnitBase* atkunitbase, int valuecount, AtkValue* atkvalues, ulong a4) {
        var atkValueList = new List<object>();
        var atkValueTypeList = new List<ValueType>();
        try {
            var a = atkvalues;
            for (var i = 0; i < valuecount; i++) {
                atkValueTypeList.Add(a->Type);
                switch (a->Type) {
                    case ValueType.Int: {
                        atkValueList.Add(a->Int);
                        break;
                    }
                    case ValueType.String: {
                        atkValueList.Add(Marshal.PtrToStringUTF8(new IntPtr(a->String)));
                        break;
                    }
                    case ValueType.UInt: {
                        atkValueList.Add(a->UInt);
                        break;
                    }
                    case ValueType.Bool: {
                        atkValueList.Add(a->Byte != 0);
                        break;
                    }
                    default: {
                        atkValueList.Add($"Unknown Type: {a->Type}");
                        break;
                    }
                }
                a++;
            }
        } catch {
            return fireCallbackHook.Original(atkunitbase, valuecount, atkvalues, a4);
        }
        var ret = fireCallbackHook.Original(atkunitbase, valuecount, atkvalues, a4);
        try {
            Callbacks.Insert(0, new Callback() {
                AtkUnitBaseName = Encoding.UTF8.GetString(atkunitbase->Name, 0x20).TrimEnd(),
                A4Value = a4,
                AtkUnitBase = atkunitbase,
                ReturnValue = ret,
                AtkValues = atkValueList,
                AtkValueTypes = atkValueTypeList,
            });

            if (Callbacks.Count > 1000) {
                Callbacks.RemoveRange(1000, Callbacks.Count - 1000);
            }

        } catch {
            //
        }

        return ret;
    }

    public override void Dispose() {
        fireCallbackHook?.Disable();
        fireCallbackHook?.Dispose();

        formatAddonTextHook?.Disable();
        formatAddonTextHook?.Dispose();

        foreach (var h in setupHooks.Values) {
            if (h.Hook.IsDisposed) continue;
            h.Hook.Disable();
            h.Hook.Dispose();
        }

        setupHooks.Clear();

        base.Dispose();
    }
}