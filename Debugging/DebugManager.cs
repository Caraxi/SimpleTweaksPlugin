﻿using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public DebugConfig Debugging = new DebugConfig();

        public bool ShouldSerializeDebugging() {
            return DebugManager.Enabled;
        }

    }
}

namespace SimpleTweaksPlugin.Debugging {

    public partial class DebugConfig {
        public string SelectedPage = String.Empty;
        public Dictionary<string, object> SavedValues = new();
    }

    public abstract class DebugHelper : IDisposable {
        public SimpleTweaksPlugin Plugin;
        public abstract void Draw();
        public abstract string Name { get; }

        public virtual void Dispose() {

        }

        public string FullName {
            get {
                if (TweakProvider is CustomTweakProvider ctp) {
                    return $"[{ctp.Assembly.GetName().Name}] {Name}";
                }
                return Name;
            }
        }

        public TweakProvider TweakProvider = null!;
    }

    public static class DebugManager {

        private static Dictionary<string, Action> debugPages = new();

        private static float sidebarSize = 0;

        public static bool Enabled = false;

        public static void RegisterDebugPage(string key, Action action) {
            if (debugPages.ContainsKey(key)) {
                debugPages[key] = action;
            } else {
                debugPages.Add(key, action);
            }

            sidebarSize = 0;
        }

        public static void RemoveDebugPage(string key) {
            if (debugPages.ContainsKey(key)) {
                debugPages.Remove(key);
            }

            sidebarSize = 0;
        }

        public static void Reload() {
            DebugHelpers.RemoveAll(dh => {
                if (!dh.TweakProvider.IsDisposed) return false;
                RemoveDebugPage(dh.FullName);
                dh.Dispose();
                return true;
            });

            foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                if (tp.IsDisposed) continue;

                foreach (var t in tp.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(DebugHelper)) && !t.IsAbstract)) {
                    if (DebugHelpers.Any(h => h.GetType() == t)) continue;
                    var debugger = (DebugHelper)Activator.CreateInstance(t);
                    debugger.TweakProvider = tp;
                    debugger.Plugin = _plugin;
                    RegisterDebugPage(debugger.FullName, debugger.Draw);
                    DebugHelpers.Add(debugger);
                }
            }


        }

        private static SimpleTweaksPlugin _plugin;

        private static bool _setupDebugHelpers = false;

        private static readonly List<DebugHelper> DebugHelpers = new List<DebugHelper>();

        public static void SetPlugin(SimpleTweaksPlugin plugin) {
            _plugin = plugin;
        }

        public static void DrawDebugWindow(ref bool open) {
            if (_plugin == null) return;
            if (!_setupDebugHelpers) {
                _setupDebugHelpers = true;
                try {
                    foreach (var tp in SimpleTweaksPlugin.Plugin.TweakProviders) {
                        if (tp.IsDisposed) continue;
                        foreach (var t in tp.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(DebugHelper)) && !t.IsAbstract)) {
                            var debugger = (DebugHelper)Activator.CreateInstance(t);
                            debugger.TweakProvider = tp;
                            debugger.Plugin = _plugin;
                            RegisterDebugPage(debugger.FullName, debugger.Draw);
                            DebugHelpers.Add(debugger);
                        }
                    }
                } catch (Exception ex) {
                    SimpleLog.Error(ex);
                    _setupDebugHelpers = false;
                    Enabled = false;
                    DebugHelpers.Clear();
                    return;
                }
            }

            if (sidebarSize < 150) {
                sidebarSize = 150;
                try {
                    foreach (var k in debugPages.Keys) {
                        var s = ImGui.CalcTextSize(k).X + ImGui.GetStyle().FramePadding.X * 5 + ImGui.GetStyle().ScrollbarSize;
                        if (s > sidebarSize) {
                            sidebarSize = s;
                        }
                    }
                } catch (Exception ex) {
                    SimpleLog.Error(ex);
                }

            }

            ImGui.SetNextWindowSize(new Vector2(500, 350) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350, 350) * ImGui.GetIO().FontGlobalScale, new Vector2(2000, 2000) * ImGui.GetIO().FontGlobalScale);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF000000);
            if (ImGui.Begin($"SimpleTweaksPlugin - Debug [{Assembly.GetExecutingAssembly().GetName().Version}]###stDebugMenu", ref open)) {

                if (ImGui.BeginChild("###debugPages", new Vector2(sidebarSize, -1) * ImGui.GetIO().FontGlobalScale, true)) {


                    var keys = debugPages.Keys.ToList();
                    keys.Sort(((s, s1) => {
                        if (s.StartsWith("[") && !s1.StartsWith("[")) {
                            return 1;
                        }
                        return string.CompareOrdinal(s, s1);
                    }));

                    foreach (var k in keys) {

                        if (ImGui.Selectable($"{k}##debugPageOption", _plugin.PluginConfig.Debugging.SelectedPage == k)) {
                            _plugin.PluginConfig.Debugging.SelectedPage = k;
                            _plugin.PluginConfig.Save();
                        }

                    }


                }

                ImGui.EndChild();
                ImGui.SameLine();

                if (ImGui.BeginChild("###debugView", new Vector2(-1, -1), true, ImGuiWindowFlags.HorizontalScrollbar)){
                    if (string.IsNullOrEmpty(_plugin.PluginConfig.Debugging.SelectedPage) || !debugPages.ContainsKey(_plugin.PluginConfig.Debugging.SelectedPage)) {
                        ImGui.Text("Select Debug Page");
                    } else {
                        try {
                            debugPages[_plugin.PluginConfig.Debugging.SelectedPage]();
                        } catch (Exception ex) {
                            SimpleLog.Error(ex);
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), ex.ToString());
                        }

                    }
                }

                ImGui.EndChild();
            }

            ImGui.End();
            ImGui.PopStyleColor();
        }

        public static void Dispose() {
            foreach (var debugger in DebugHelpers) {
                RemoveDebugPage(debugger.FullName);
                debugger.Dispose();
            }
            DebugHelpers.Clear();
            debugPages.Clear();
        }


        private static unsafe Vector2 GetNodePosition(AtkResNode* node) {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null) {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }
            return pos;
        }

        private static unsafe Vector2 GetNodeScale(AtkResNode* node) {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null) {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }
            return scale;
        }

        private static unsafe bool GetNodeVisible(AtkResNode* node) {
            if (node == null) return false;
            while (node != null) {
                if ((node->Flags & (short)NodeFlags.Visible) != (short)NodeFlags.Visible) return false;
                node = node->ParentNode;
            }
            return true;
        }

        public static unsafe void HighlightResNode(AtkResNode* node) {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;

            var nodeVisible = GetNodeVisible(node);
            ImGui.GetForegroundDrawList().AddRectFilled(position, position+size, (uint) (nodeVisible ? 0x5500FF00 : 0x550000FF));
            ImGui.GetForegroundDrawList().AddRect(position, position + size, nodeVisible ? 0xFF00FF00 : 0xFF0000FF);

        }

        public static void ClickToCopyText(string text, string textCopy = null) {
            textCopy ??= text;
            ImGui.Text($"{text}");
            if (ImGui.IsItemHovered()) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (textCopy != text) ImGui.SetTooltip(textCopy);
            }
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{textCopy}");
        }

        public static unsafe void ClickToCopy(void* address) {
            ClickToCopyText($"{(ulong)address:X}");
        }
        public static unsafe void ClickToCopy<T>(T* address) where T : unmanaged {
            ClickToCopy((void*) address);
        }

        public static unsafe void SeStringToText(SeString seStr) {
            var pushColorCount = 0;

            ImGui.BeginGroup();
            foreach (var p in seStr.Payloads) {
                switch (p) {
                    case UIForegroundPayload c:
                        if (c.ColorKey == 0) {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                            pushColorCount++;
                            break;
                        }
                        var r = (c.UIColor.UIForeground >> 0x18) & 0xFF;
                        var g = (c.UIColor.UIForeground >> 0x10) & 0xFF;
                        var b = (c.UIColor.UIForeground >> 0x08) & 0xFF;
                        var a = (c.UIColor.UIForeground >> 0x00) & 0xFF;

                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(r / 255f, g / 255f, b / 255f, 1));
                        pushColorCount++;
                        break;
                    case TextPayload t:
                        ImGui.Text($"{t.Text}");
                        ImGui.SameLine();
                        break;
                }
            }
            ImGui.EndGroup();
            if (pushColorCount > 0) ImGui.PopStyleColor(pushColorCount);
        }

        private static ulong beginModule = 0;
        private static ulong endModule = 0;

        private static unsafe void PrintOutValue(ulong addr, List<string> path, Type type, object value, MemberInfo member) {
            try {

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
                    value = type.GetMethod("ToArray")?.Invoke(value, null);
                    type = value.GetType();
                }
                
                var valueParser = member.GetCustomAttribute(typeof(ValueParser));
                var fixedBuffer = (FixedBufferAttribute) member.GetCustomAttribute(typeof(FixedBufferAttribute));
                var fixedArray = (FixedArrayAttribute)member.GetCustomAttribute(typeof(FixedArrayAttribute));
                
                if (valueParser is ValueParser vp) {
                    vp.ImGuiPrint(type, value, member, addr);
                    return;
                }

                if (type.IsPointer) {
                    var val = (Pointer) value;
                    var unboxed = Pointer.Unbox(val);
                    if (unboxed != null) {
                        var unboxedAddr = (ulong) unboxed;
                        ClickToCopyText($"{(ulong) unboxed:X}");
                        if (beginModule > 0 && unboxedAddr >= beginModule && unboxedAddr <= endModule) {
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                            ClickToCopyText($"ffxiv_dx11.exe+{(unboxedAddr - beginModule):X}");
                            ImGui.PopStyleColor();
                        }

                        try {
                            var eType = type.GetElementType();
                            var ptrObj = Marshal.PtrToStructure(new IntPtr(unboxed), eType);
                            ImGui.SameLine();
                            PrintOutObject(ptrObj, (ulong) unboxed, new List<string>(path));
                        } catch {
                            // Ignored
                        }
                    } else {
                        ImGui.Text("null");
                    }
                } else {

                    if (type.IsArray) {

                        var arr = (Array) value;
                        if (ImGui.TreeNode($"Values##{member.Name}-{addr}-{string.Join("-", path)}")) {
                            for (var i = 0; i < arr.Length; i++) {
                                ImGui.Text($"[{i}]");
                                ImGui.SameLine();
                                PrintOutValue(addr, new List<string>(path) { $"_arrValue_{i}" }, type.GetElementType(), arr.GetValue(i), member);
                            }
                            ImGui.TreePop();
                        }


                    } else if (fixedBuffer != null) {
                        
                        
                        if (fixedArray != null) {

                            if (ImGui.TreeNode($"Fixed {fixedArray.Type.Name} Array##{member.Name}-{addr}-{string.Join("-", path)}")) {

                                var arrAddr = (IntPtr) addr;
                                for (var i = 0; i < fixedArray.Count; i++) {
                                    var arrObj = Marshal.PtrToStructure(arrAddr, fixedArray.Type);
                                    PrintOutObject(arrObj, (ulong)arrAddr.ToInt64(), new List<string>(path) { $"_arrValue_{i}" }, false, $"[{i}] {arrObj}");
                                    arrAddr += Marshal.SizeOf(fixedArray.Type);
                                }

                                ImGui.TreePop();
                            }
                            
                        } else {
                        
                            if (ImGui.TreeNode($"Fixed {fixedBuffer.ElementType.Name} Buffer##{member.Name}-{addr}-{string.Join("-", path)}")) {
                                var display = true;
                                var child = false;
                                if (fixedBuffer.ElementType == typeof(byte) && fixedBuffer.Length > 0x80) {
                                    display = ImGui.BeginChild($"scrollBuffer##{member.Name}-{addr}-{string.Join("-", path)}", new Vector2(ImGui.GetTextLineHeight() * 30, ImGui.GetTextLineHeight() * 8), true);
                                    child = true;
                                }

                                if (display) {
                                    var sX = ImGui.GetCursorPosX();
                                    for (uint i = 0; i < fixedBuffer.Length; i++) {
                                        if (fixedBuffer.ElementType == typeof(byte)) {
                                            var v = *(byte*)(addr + i);
                                            if (i != 0 && i % 16 != 0) ImGui.SameLine();
                                            ImGui.SetCursorPosX(sX + ImGui.CalcTextSize(ImGui.GetIO().KeyShift?"0000":"000").X * (i % 16));
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000}" : $"{v:X2}");
                                        } else if (fixedBuffer.ElementType == typeof(short)) {
                                            var v = *(short*)(addr + i);
                                            if (i != 0 && i % 8 != 0) ImGui.SameLine();
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000000}" : $"{v:X4}");
                                        } else if (fixedBuffer.ElementType == typeof(ushort)) {
                                            var v = *(ushort*)(addr + i);
                                            if (i != 0 && i % 8 != 0) ImGui.SameLine();
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:00000}" : $"{v:X4}");
                                        }  else if (fixedBuffer.ElementType == typeof(int)) {
                                            var v = *(int*)(addr + i);
                                            if (i != 0 && i % 4 != 0) ImGui.SameLine();
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:0000000000}" : $"{v:X8}");
                                        }  else if (fixedBuffer.ElementType == typeof(uint)) {
                                            var v = *(uint*)(addr + i);
                                            if (i != 0 && i % 4 != 0) ImGui.SameLine();
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000000000}" : $"{v:X8}");
                                        } else if (fixedBuffer.ElementType == typeof(long)) {
                                            var v = *(long*)(addr + i);
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v}" : $"{v:X16}");
                                        } else if (fixedBuffer.ElementType == typeof(ulong)) {
                                            var v = *(ulong*)(addr + i);
                                            ImGui.Text(ImGui.GetIO().KeyShift ? $"{v}" : $"{v:X16}");
                                        } else {
                                            var v = *(byte*)(addr + i);
                                            if (i != 0 && i % 16 != 0) ImGui.SameLine();
                                            ImGui.TextDisabled(ImGui.GetIO().KeyShift ? $"{v:000}" : $"{v:X2}");
                                        }
          
                                    }
                                }

                                if (child) {
                                    ImGui.EndChild();
                                }
                                
                                ImGui.TreePop();
                            }
                        }
                    } else if (!type.IsPrimitive) {
                        switch (value) {
                            case ILazyRow ilr:
                                var p = ilr.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                                if (p != null) {
                                    var getter = p.GetGetMethod();
                                    if (getter != null) {
                                        var rowValue = getter.Invoke(ilr, new object?[] { });
                                        PrintOutObject(rowValue, addr, new List<string>(path));
                                        break;
                                    }
                                }
                                PrintOutObject(value, addr, new List<string>(path));
                                break;
                            case Lumina.Text.SeString seString:
                                ImGui.Text($"{seString.RawString}");
                                break;
                            default:
                                PrintOutObject(value, addr, new List<string>(path));
                                break;
                        }
                    } else {
                        if (value is IntPtr p) {
                            var pAddr = (ulong)p.ToInt64();
                            ClickToCopyText($"{p:X}");
                            if (beginModule > 0 && pAddr >= beginModule && pAddr <= endModule) {
                                ImGui.SameLine();
                                ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                                ClickToCopyText($"ffxiv_dx11.exe+{(pAddr - beginModule):X}");
                                ImGui.PopStyleColor();
                            }
                        } else {
                            ImGui.Text($"{value}");
                        }

                    }
                }
            } catch (Exception ex) {
                ImGui.Text($"{{{ex}}}");
            }

        }

        public static unsafe void PrintOutObject<T>(T* ptr, bool autoExpand = false, string headerText = null) where T : unmanaged {
            PrintOutObject(ptr, new List<string>(), autoExpand, headerText);
        }

        public static unsafe void PrintOutObject<T>(T* ptr, List<string> path, bool autoExpand = false, string headerText = null) where T : unmanaged {
            PrintOutObject(*ptr, (ulong) ptr, path, autoExpand, headerText);
        }

        public static unsafe void PrintOutObject(object obj, ulong addr, bool autoExpand = false, string headerText = null) {
            PrintOutObject(obj, addr, new List<string>(), autoExpand, headerText);
        }

        private static Dictionary<string, object> _savedValues = new();
        public static void SetSavedValue<T>(string key, T value){
            if (_plugin.PluginConfig.Debugging.SavedValues.ContainsKey(key)) _plugin.PluginConfig.Debugging.SavedValues.Remove(key);
            _plugin.PluginConfig.Debugging.SavedValues.Add(key, value);
            _plugin.PluginConfig.Save();
        }

        public static T GetSavedValue<T>(string key, T defaultValue) {
            if (!_plugin.PluginConfig.Debugging.SavedValues.ContainsKey(key)) return defaultValue;
            return (T)_plugin.PluginConfig.Debugging.SavedValues[key];
        }

        public static unsafe void PrintOutObject(object obj, ulong addr, List<string> path, bool autoExpand = false, string headerText = null) {
            if (obj is Utf8String utf8String) {

                var text = string.Empty;
                Exception err = null;
                try {
                    var s = utf8String.BufUsed > int.MaxValue ? int.MaxValue : (int) utf8String.BufUsed;
                    if (s > 1) {
                        text = Encoding.UTF8.GetString(utf8String.StringPtr, s - 1);
                    }
                } catch (Exception ex) {
                    err = ex;
                }


                if (err != null) {
                    ImGui.TextDisabled(err.Message);
                    ImGui.SameLine();
                } else {
                    ImGui.Text($"\"{text}\"");
                    ImGui.SameLine();
                }

            }
            
            var pushedColor = 0;
            var openedNode = false;
            try {
                if (endModule == 0 && beginModule == 0) {
                    try {
                        beginModule = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                        endModule = (beginModule + (ulong)Process.GetCurrentProcess().MainModule.ModuleMemorySize);
                    } catch {
                        endModule = 1;
                    }
                }

                ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FFFF);
                pushedColor++;
                if (autoExpand) {
                    ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
                }

                headerText ??= $"{obj}";
                



                if (ImGui.TreeNode($"{headerText}##print-obj-{addr:X}-{string.Join("-", path)}")) {

                    var layoutKind = obj.GetType().StructLayoutAttribute?.Value ?? LayoutKind.Sequential;
                    var offsetAddress = 0UL;
                    openedNode = true;
                    ImGui.PopStyleColor();
                    pushedColor--;
                    
                    foreach (var f in obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance)) {

                        if (f.IsStatic) {
                            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.75f, 1f), "static");
                            ImGui.SameLine();
                        } else {
                            if (layoutKind == LayoutKind.Explicit) {
                                if (f.GetCustomAttribute(typeof(FieldOffsetAttribute)) is FieldOffsetAttribute o) {
                                    offsetAddress = (ulong)o.Value;
                                }
                            }
                        
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFF888888);
                            ClickToCopyText($"[0x{offsetAddress:X}]", $"{(addr + offsetAddress):X}");
                            ImGui.PopStyleColor();
                            ImGui.SameLine();
                        }
                        
                        
                        
                        var fixedBuffer = (FixedBufferAttribute) f.GetCustomAttribute(typeof(FixedBufferAttribute));
                        if (fixedBuffer != null) {
                            var fixedArray = (FixedArrayAttribute)f.GetCustomAttribute(typeof(FixedArrayAttribute));
                            ImGui.Text($"fixed");
                            ImGui.SameLine();
                            if (fixedArray != null) {
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{fixedArray.Type.Name}[{fixedArray.Count:X}]");
                            } else {
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{fixedBuffer.ElementType.Name}[0x{fixedBuffer.Length:X}]");
                            }
                        } else {
                            
                            if (f.FieldType.IsArray) {
                                var arr = (Array) f.GetValue(obj);
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{f.FieldType.GetElementType()?.Name ?? f.FieldType.Name}[{arr.Length}]");
                            } else {
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{f.FieldType.Name}");
                            }
                        }

                        ImGui.SameLine();

                        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1), $"{f.Name}: ");
                        ImGui.SameLine();


                        if (fixedBuffer != null) {
                            PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, f.FieldType, f.GetValue(obj), f);
                        } else {
                            PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, f.FieldType, f.GetValue(obj), f);
                        }

                        if (layoutKind == LayoutKind.Sequential && f.IsStatic == false) {
                            offsetAddress += (ulong)Marshal.SizeOf(f.FieldType);
                        }
                    }

                    foreach (var p in obj.GetType().GetProperties()) {
                        
                        if (p.PropertyType.IsGenericType) {
                            var gTypeName = string.Join(',', p.PropertyType.GetGenericArguments().Select(gt => gt.Name));
                            var baseName = p.PropertyType.Name.Split('`')[0];
                            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{baseName}<{gTypeName}>");
                        } else {
                            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{p.PropertyType.Name}");
                        }

                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.2f, 0.6f, 0.4f, 1), $"{p.Name}: ");
                        ImGui.SameLine();

                        if (p.PropertyType.IsByRefLike) {
                            ImGui.TextDisabled("Unable to display");
                        } else {
                            PrintOutValue(addr, new List<string>(path) { p.Name }, p.PropertyType, p.GetValue(obj), p);
                        }
                        
                        
                    }

                    openedNode = false;
                    ImGui.TreePop();
                } else {
                    ImGui.PopStyleColor();
                    pushedColor--;
                }
            } catch (Exception ex) {
                ImGui.Text($"{{{ ex }}}");
            }

            if (openedNode) ImGui.TreePop();
            if (pushedColor > 0) ImGui.PopStyleColor(pushedColor);

        }

        public static unsafe void PrintAddress(void* address) {
            var ulongAddress = (ulong)address;
            
            try {
                if (endModule == 0 && beginModule == 0) {
                    try {
                        beginModule = (ulong)Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                        endModule = (beginModule + (ulong)Process.GetCurrentProcess().MainModule.ModuleMemorySize);
                    } catch {
                        endModule = 1;
                    }
                }
            } catch { }
            
            ClickToCopy(address);
            
            if (beginModule > 0 && ulongAddress >= beginModule && ulongAddress <= endModule) {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                ClickToCopyText($"ffxiv_dx11.exe+{(ulongAddress - beginModule):X}");
                ImGui.PopStyleColor();
            }
        }
        
    }
}
