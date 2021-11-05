using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Debugging {
    public unsafe class AddonCallbacks : DebugHelper {
        public override string Name => "Addon Logging";

        private delegate void* FireCallbackDelegate(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues, ulong a4);
        private HookWrapper<FireCallbackDelegate> fireCallbackHook;
        private bool enabled = false;

        public delegate void* OnSetupDelegate(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues);
        private static Dictionary<string, SetupHook> setupHooks = new();

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
                Hook = Common.Hook<OnSetupDelegate>(unitBase->AtkEventListener.vfunc[43], SetupDetour);
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
                            case ValueType.String: {
                                atkValueList.Add(Marshal.PtrToStringUTF8(new IntPtr(a->String)));
                                break;
                            }
                            case ValueType.UInt: {
                                atkValueList.Add(a->UInt);
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
                            fireCallbackHook ??= Common.Hook<FireCallbackDelegate>("48 8B C4 44 88 48 20 53", CallbackDetour);
                            fireCallbackHook?.Enable();
                        }
                    }

                    ImGui.Separator();

                    if (ImGui.BeginTable("callbacksTable", 4, ImGuiTableFlags.RowBg)) {
                        ImGui.TableSetupColumn("Addon", ImGuiTableColumnFlags.WidthFixed, 150);
                        ImGui.TableSetupColumn("Values", ImGuiTableColumnFlags.WidthFixed, 300);
                        ImGui.TableSetupColumn("A4", ImGuiTableColumnFlags.WidthFixed, 150);
                        ImGui.TableSetupColumn("Return", ImGuiTableColumnFlags.WidthFixed, 60);

                        ImGui.TableHeadersRow();

                        foreach (var cb in Callbacks) {
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


                ImGui.EndTabBar();
            }
        }

        public static void HookOnSetup(AtkUnitBase* atkUnitBase) {
            var name = Encoding.UTF8.GetString(atkUnitBase->Name, 0x20).TrimEnd();
            setupHooks.Add(name, new SetupHook(atkUnitBase));
        }

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
            } catch {
                //
            }

            return ret;
        }

        public override void Dispose() {
            fireCallbackHook?.Disable();
            fireCallbackHook?.Dispose();

            foreach (var h in setupHooks.Values) {
                if (h.Hook.IsDisposed) continue;
                h.Hook.Disable();
                h.Hook.Dispose();
            }

            setupHooks.Clear();

            base.Dispose();
        }
    }
}
