using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using InteropGenerator.Runtime.Attributes;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public DebugConfig Debugging = new DebugConfig();
    }
}

namespace SimpleTweaksPlugin.Debugging {
    public partial class DebugConfig {
        public string SelectedPage = String.Empty;
        public Dictionary<string, object> SavedValues = new();

        public List<string> Undocked = new List<string>();
    }

    public abstract class DebugHelper : IDisposable {
        public SimpleTweaksPlugin Plugin;
        public abstract void Draw();
        public abstract string Name { get; }

        public virtual void Dispose() { }

        public virtual void Reload() { }

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

        private static float sidebarSize;

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
                    try {
                        if (DebugHelpers.Any(h => h.GetType() == t)) continue;
                        var debugger = (DebugHelper)Activator.CreateInstance(t);
                        if (debugger != null) {
                            SignatureHelper.Initialise(debugger);
                            debugger.TweakProvider = tp;
                            debugger.Plugin = _plugin;
                            RegisterDebugPage(debugger.FullName, debugger.Draw);
                            DebugHelpers.Add(debugger);
                        }
                    } catch (Exception ex) {
                        SimpleLog.Error(ex, $"Failed to register debug page with type {t.FullName}");
                    }
                }
            }
            
            DebugHelpers.ForEach(dh => dh.Reload());
        }

        private static SimpleTweaksPlugin _plugin;

        private static bool _setupDebugHelpers;

        private static readonly List<DebugHelper> DebugHelpers = new List<DebugHelper>();

        public static void SetPlugin(SimpleTweaksPlugin plugin) {
            _plugin = plugin;
        }

        static DebugManager() {
            Service.PluginInterface.UiBuilder.Draw += DrawUndockedPages;
        }

        private static Stopwatch initDelay = Stopwatch.StartNew();

        public static void DrawDebugWindow() {
            if (initDelay.ElapsedMilliseconds < 500) return;
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
                    DebugHelpers.Clear();
                    _plugin.DebugWindow.IsOpen = false;
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

            if (ImGui.BeginChild("###debugPages", new Vector2(sidebarSize, -1) * ImGui.GetIO().FontGlobalScale, true)) {
                var keys = debugPages.Keys.ToList();
                keys.Sort(((s, s1) => {
                    if (s.StartsWith("[") && !s1.StartsWith("[")) {
                        return 1;
                    }

                    return string.CompareOrdinal(s, s1);
                }));

                foreach (var k in keys) {
                    using var _ = ImRaii.Disabled(_plugin.PluginConfig.Debugging.Undocked.Contains(k));

                    if (ImGui.Selectable($"{k}##debugPageOption", _plugin.PluginConfig.Debugging.SelectedPage == k)) {
                        _plugin.PluginConfig.Debugging.SelectedPage = k;
                        _plugin.PluginConfig.Save();
                    }

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
                        if (!_plugin.PluginConfig.Debugging.Undocked.Remove(k)) {
                            _plugin.PluginConfig.Debugging.Undocked.Add(k);
                        }
                    }
                }
            }

            ImGui.EndChild();
            ImGui.SameLine();

            if (ImGui.BeginChild("###debugView", new Vector2(-1, -1), true, ImGuiWindowFlags.HorizontalScrollbar)) {
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

        public static void Dispose() {
            foreach (var debugger in DebugHelpers) {
                RemoveDebugPage(debugger.FullName);
                debugger.Dispose();
            }

            DebugHelpers.Clear();
            debugPages.Clear();
            Service.PluginInterface.UiBuilder.Draw -= DrawUndockedPages;
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
                if (!node->IsVisible()) return false;
                node = node->ParentNode;
            }

            return true;
        }

        public static unsafe void HighlightResNode(AtkResNode* node) {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;

            var nodeVisible = GetNodeVisible(node);
            ImGui.GetForegroundDrawList().AddRectFilled(position, position + size, (uint)(nodeVisible ? 0x5500FF00 : 0x550000FF));
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
            ClickToCopy((void*)address);
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

                        var r = (c.UIColor.Value.UIForeground >> 0x18) & 0xFF;
                        var g = (c.UIColor.Value.UIForeground >> 0x10) & 0xFF;
                        var b = (c.UIColor.Value.UIForeground >> 0x08) & 0xFF;
                        var a = (c.UIColor.Value.UIForeground >> 0x00) & 0xFF;

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

        private static ulong beginModule;
        private static ulong endModule;

        private static unsafe void PrintOutValue(ulong addr, List<string> path, Type type, object value, MemberInfo member) {
            try {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
                    value = type.GetMethod("ToArray")?.Invoke(value, null);
                    type = value.GetType();
                }

                var valueParser = member.GetCustomAttribute(typeof(ValueParser));
                var fixedBuffer = (FixedBufferAttribute)member.GetCustomAttribute(typeof(FixedBufferAttribute));

                if (valueParser is ValueParser vp) {
                    vp.ImGuiPrint(type, value, member, addr);
                    return;
                }

                if (type.IsPointer) {
                    var val = (Pointer)value;
                    var unboxed = Pointer.Unbox(val);
                    if (unboxed != null) {
                        var unboxedAddr = (ulong)unboxed;
                        ClickToCopyText($"{(ulong)unboxed:X}");
                        if (beginModule > 0 && unboxedAddr >= beginModule && unboxedAddr <= endModule) {
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                            ClickToCopyText($"ffxiv_dx11.exe+{(unboxedAddr - beginModule):X}");
                            ImGui.PopStyleColor();
                        }

                        try {
                            var eType = type.GetElementType();
                            var ptrObj = SafeMemory.PtrToStructure(new IntPtr(unboxed), eType);
                            ImGui.SameLine();
                            PrintOutObject(ptrObj, (ulong)unboxed, new List<string>(path));
                        } catch {
                            // Ignored
                        }
                    } else {
                        ImGui.Text("null");
                    }
                } else {
                    if (type.IsArray) {
                        var arr = (Array)value;
                        if (ImGui.TreeNode($"Values##{member.Name}-{addr}-{string.Join("-", path)}")) {
                            for (var i = 0; i < arr.Length; i++) {
                                ImGui.Text($"[{i}]");
                                ImGui.SameLine();
                                PrintOutValue(addr, new List<string>(path) { $"_arrValue_{i}" }, type.GetElementType(), arr.GetValue(i), member);
                            }

                            ImGui.TreePop();
                        }
                    } else if (fixedBuffer != null) {
                        if (ImGui.TreeNode($"Fixed {ParseTypeName(fixedBuffer.ElementType)} Buffer##{member.Name}-{addr}-{string.Join("-", path)}")) {
                            var display = true;
                            var child = false;
                            if (fixedBuffer.ElementType == typeof(byte) && fixedBuffer.Length > 0x80) {
                                display = ImGui.BeginChild($"scrollBuffer##{member.Name}-{addr}-{string.Join("-", path)}", new Vector2(ImGui.GetTextLineHeight() * 30, ImGui.GetTextLineHeight() * 8), true);
                                child = true;
                            }

                            if (display) {
                                var sX = ImGui.GetCursorPosX();
                                for (uint i = 0; i < fixedBuffer.Length; i += 1) {
                                    if (fixedBuffer.ElementType == typeof(byte)) {
                                        var v = *(byte*)(addr + i);
                                        if (i != 0 && i % 16 != 0) ImGui.SameLine();
                                        ImGui.SetCursorPosX(sX + ImGui.CalcTextSize(ImGui.GetIO().KeyShift ? "0000" : "000").X * (i % 16));
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000}" : $"{v:X2}");
                                    } else if (fixedBuffer.ElementType == typeof(short)) {
                                        var v = *(short*)(addr + i * 2);
                                        if (i != 0 && i % 8 != 0) ImGui.SameLine();
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000000}" : $"{v:X4}");
                                    } else if (fixedBuffer.ElementType == typeof(ushort)) {
                                        var v = *(ushort*)(addr + i * 2);
                                        if (i != 0 && i % 8 != 0) ImGui.SameLine();
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:00000}" : $"{v:X4}");
                                    } else if (fixedBuffer.ElementType == typeof(int)) {
                                        var v = *(int*)(addr + i * 4);
                                        if (i != 0 && i % 4 != 0) ImGui.SameLine();
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:0000000000}" : $"{v:X8}");
                                    } else if (fixedBuffer.ElementType == typeof(uint)) {
                                        var v = *(uint*)(addr + i * 4);
                                        if (i != 0 && i % 4 != 0) ImGui.SameLine();
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v:000000000}" : $"{v:X8}");
                                    } else if (fixedBuffer.ElementType == typeof(long)) {
                                        var v = *(long*)(addr + i * 8);
                                        ImGui.Text(ImGui.GetIO().KeyShift ? $"{v}" : $"{v:X16}");
                                    } else if (fixedBuffer.ElementType == typeof(ulong)) {
                                        var v = *(ulong*)(addr + i * 8);
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
                    } else if (!type.IsPrimitive) {
                        switch (value) {
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
            PrintOutObject(*ptr, (ulong)ptr, path, autoExpand, headerText);
        }

        public static unsafe void PrintOutObject(object obj, ulong addr, bool autoExpand = false, string headerText = null) {
            PrintOutObject(obj, addr, new List<string>(), autoExpand, headerText);
        }

        private static Dictionary<string, object> _savedValues = new();

        public static void SetSavedValue<T>(string key, T value) {
            if (_plugin.PluginConfig.Debugging.SavedValues.ContainsKey(key)) _plugin.PluginConfig.Debugging.SavedValues.Remove(key);
            _plugin.PluginConfig.Debugging.SavedValues.Add(key, value);
            _plugin.PluginConfig.Save();
        }

        public static T GetSavedValue<T>(string key, T defaultValue) {
            if (!_plugin.PluginConfig.Debugging.SavedValues.ContainsKey(key)) return defaultValue;
            return (T)_plugin.PluginConfig.Debugging.SavedValues[key];
        }

        private static string ParseTypeName(Type type, List<Type> loopSafety = null) {
            if (!type.IsGenericType) return type.Name;
            loopSafety ??= new List<Type>();
            if (loopSafety.Contains(type)) return $"...{type.Name}";
            loopSafety.Add(type);
            var n = type.Name.Split('`')[0];
            var gArgs = type.GetGenericArguments().Select(t => $"{ParseTypeName(t, loopSafety)}");
            return $"{n}<{string.Join(',', gArgs)}>";
        }

        private static unsafe void PrintOutField(FieldInfo f, LayoutKind layoutKind, ref ulong offsetAddress, ulong addr, object obj, List<string> path, string customTypeName = null, string customName = null) {
            var fixedSizeArrayAttribute = f.GetCustomAttribute<FixedSizeArrayAttribute>();
            if (fixedSizeArrayAttribute?.IsString ?? false) return;

            if (ImGui.GetIO().KeyShift) {
                foreach (var a in f.CustomAttributes) {
                    if (a.AttributeType == typeof(FieldOffsetAttribute)) continue;
                    ImGui.TextColored(ImGuiColors.DalamudOrange, $"[{a.AttributeType}]");
                    ImGui.SameLine();
                }
            }

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

                var addressText = GetAddressString((void*)(addr + offsetAddress), ImGui.GetIO().KeyShift);
                ClickToCopyText($"[0x{offsetAddress:X}]", addressText);
                ImGui.PopStyleColor();
                ImGui.SameLine();
            }

            int fixedSizeArraySize;
            try {
                fixedSizeArraySize = fixedSizeArrayAttribute != null ? int.Parse(f.FieldType.Name.Split("FixedSizeArray", 2).Last().Split("`").First()) : 0;
            } catch {
                fixedSizeArraySize = 0;
            }

            var fixedBuffer = (FixedBufferAttribute)f.GetCustomAttribute(typeof(FixedBufferAttribute));

            if (fixedBuffer != null) {
                ImGui.Text($"fixed");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{fixedBuffer.ElementType.Name}[0x{fixedBuffer.Length:X}]");
            } else {
                if (fixedSizeArrayAttribute != null) {
                    ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{customTypeName ?? ParseTypeName(f.FieldType.GetElementType() ?? f.FieldType)}[{fixedSizeArraySize}]");
                } else if (f.FieldType.IsArray) {
                    var arr = (Array)f.GetValue(obj);
                    ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{customTypeName ?? ParseTypeName(f.FieldType.GetElementType() ?? f.FieldType)}[{arr?.Length ?? 0}]");
                } else {
                    ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{customTypeName ?? ParseTypeName(f.FieldType)}");
                }
            }

            ImGui.SameLine();

            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1), $"{customName ?? f.Name}: ");
            var fullFieldName = $"{(obj.GetType().FullName ?? "UnknownType")}.{f.Name}";
            if (ImGui.GetIO().KeyShift && ImGui.IsItemHovered()) {
                ImGui.SetTooltip(fullFieldName);
            }

            if (ImGui.GetIO().KeyShift && ImGui.IsItemClicked()) {
                ImGui.SetClipboardText(fullFieldName);
            }

            ImGui.SameLine();

            if (fixedSizeArrayAttribute != null) {
                bool isOpen;
                using (ImRaii.PushColor(ImGuiCol.Text, 0xFF00FFFF)) {
                    isOpen = ImGui.TreeNode($"{customTypeName ?? ParseTypeName(f.FieldType)}[{fixedSizeArraySize}]##print-fixedSizeArray-{addr + offsetAddress:X}-{string.Join("-", path)}");
                }

                if (isOpen) {
                    if (f.FieldType.GenericTypeArguments[0].IsPrimitive) {
                        var typeSize = (uint)Marshal.SizeOf(f.FieldType.GenericTypeArguments[0]);
                        for (var i = 0U; i < fixedSizeArraySize; i++) {
                            var vAddr = addr + offsetAddress + i * typeSize;
                            var s = Marshal.PtrToStructure((nint)vAddr, f.FieldType.GenericTypeArguments[0]);

                            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))) {
                                ClickToCopyText($"[{i.ToString().PadLeft((fixedSizeArraySize - 1).ToString().Length, '0')}]", $"{vAddr:X}");
                            }

                            ImGui.SameLine();
                            ImGui.Text($"{s}");
                        }
                    } else if (f.FieldType.GenericTypeArguments[0].FullName!.StartsWith("FFXIVClientStructs.Interop.Pointer`1")) {
                        for (var i = 0U; i < fixedSizeArraySize; i++) {
                            var vAddr = addr + offsetAddress + i * 8;
                            var ptr = *(ulong*)vAddr;
                            var s = Marshal.PtrToStructure((nint)ptr, f.FieldType.GenericTypeArguments[0].GenericTypeArguments[0]);
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))) {
                                ClickToCopyText($"[{i.ToString().PadLeft((fixedSizeArraySize - 1).ToString().Length, '0')}]", $"{vAddr:X}");
                            }

                            ImGui.SameLine();
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange)) {
                                PrintAddress((void*)ptr);
                            }

                            ImGui.SameLine();
                            PrintOutObject(s, ptr, [..path, $"{i}"], false, $"[{i}] {ParseTypeName(f.FieldType.GenericTypeArguments[0].GenericTypeArguments[0])}");
                        }
                    } else {
                        var typeSize = (uint)Marshal.SizeOf(f.FieldType.GenericTypeArguments[0]);
                        for (var i = 0U; i < fixedSizeArraySize; i++) {
                            var vAddr = addr + offsetAddress + i * typeSize;
                            var s = Marshal.PtrToStructure((nint)vAddr, f.FieldType.GenericTypeArguments[0]);
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))) {
                                ClickToCopyText($"[{i.ToString().PadLeft((fixedSizeArraySize - 1).ToString().Length - 1, '0')}]", $"{vAddr:X}");
                            }

                            ImGui.SameLine();
                            PrintOutObject(s, vAddr, [..path, $"{i}"]);
                        }
                    }

                    ImGui.TreePop();
                }
            } else {
                switch (fullFieldName) {
                    case "FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject.Name" when fixedBuffer != null:
                        var str = MemoryHelper.ReadSeString((nint)(addr + offsetAddress), fixedBuffer.Length);
                        // PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, typeof(SeString), str, f);
                        PrintOutObject(str, addr + offsetAddress);
                        break;
                    default:
                        if (fixedBuffer != null) {
                            PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, f.FieldType, f.GetValue(obj), f);
                        } else {
                            if (f.FieldType == typeof(bool) && fullFieldName.StartsWith("FFXIVClientStructs.FFXIV")) {
                                var b = *(byte*)(addr + offsetAddress);
                                PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, f.FieldType, b != 0, f);
                            } else {
                                PrintOutValue(addr + offsetAddress, new List<string>(path) { f.Name }, f.FieldType, f.GetValue(obj), f);
                            }
                        }

                        break;
                }
            }

            if (layoutKind == LayoutKind.Sequential && f.IsStatic == false) {
                if (!f.FieldType.IsGenericType) {
                    offsetAddress += (ulong)Marshal.SizeOf(f.FieldType);
                }
                
            }
        }

        public static void PrintOutProperty(PropertyInfo p, LayoutKind layoutKind, ref ulong offsetAddress, ulong addr, object obj, List<string> path) {
            if ((p.PropertyType.IsByRefLike || p.GetMethod == null || p.GetMethod.GetParameters().Length > 0) && p.PropertyType.Name == "Span`1") {
                var fieldName = $"_{p.Name[..1].ToLower()}{p.Name[1..]}";
                var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) {
                    PrintOutField(field, layoutKind, ref offsetAddress, addr, obj, path, ParseTypeName(p.PropertyType), p.Name);
                    return;
                }
            }

            if (ImGui.GetIO().KeyShift) {
                foreach (var a in p.CustomAttributes) {
                    if (a.AttributeType == typeof(FieldOffsetAttribute) || a.AttributeType == typeof(UnscopedRefAttribute)) continue;
                    ImGui.TextColored(ImGuiColors.DalamudOrange, $"[{a.AttributeType}]");
                    ImGui.SameLine();
                }
            }

            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{ParseTypeName(p.PropertyType)}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 0.6f, 0.4f, 1), $"{p.Name}: ");
            ImGui.SameLine();

            if (p.PropertyType.IsByRefLike || p.GetMethod == null || p.GetMethod.GetParameters().Length > 0) {
                ImGui.TextDisabled("Unable to display");
            } else {
                PrintOutValue(addr, new List<string>(path) { p.Name }, p.PropertyType, p.GetValue(obj), p);
            }
        }

        public static unsafe void PrintOutObject(object obj, ulong addr, List<string> path, bool autoExpand = false, string headerText = null) {
            if (obj is Utf8String utf8String) {
                var text = string.Empty;
                Exception err = null;
                try {
                    var s = utf8String.BufUsed > int.MaxValue ? int.MaxValue : (int)utf8String.BufUsed;
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
                        beginModule = (ulong)Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
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

                    foreach (var f in obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | (ImGui.GetIO().KeyShift ? BindingFlags.NonPublic : BindingFlags.Public))) {
                        PrintOutField(f, layoutKind, ref offsetAddress, addr, obj, path);
                    }

                    foreach (var p in obj.GetType().GetProperties()) {
                        PrintOutProperty(p, layoutKind, ref offsetAddress, addr, obj, path);
                    }

                    openedNode = false;
                    ImGui.TreePop();
                } else {
                    ImGui.PopStyleColor();
                    pushedColor--;
                }
            } catch (Exception ex) {
                ImGui.Text($"{{{ex}}}");
            }

            if (openedNode) ImGui.TreePop();
            if (pushedColor > 0) ImGui.PopStyleColor(pushedColor);
        }

        public static unsafe string GetAddressString(void* address, out bool isRelative, bool absoluteOnly = false) {
            var ulongAddress = (ulong)address;
            isRelative = false;
            if (absoluteOnly) return $"{ulongAddress:X}";

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

            if (beginModule > 0 && ulongAddress >= beginModule && ulongAddress <= endModule) {
                isRelative = true;
                return $"ffxiv_dx11.exe+{(ulongAddress - beginModule):X}";
            }

            return $"{ulongAddress:X}";
        }

        public static unsafe string GetAddressString(void* address, bool absoluteOnly = false) => GetAddressString(address, out _, absoluteOnly);

        public static unsafe void PrintAddress(void* address) {
            var addressString = GetAddressString(address, out var isRelative);
            if (isRelative) {
                var absoluteString = GetAddressString(address, true);
                ClickToCopyText(absoluteString);
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                ClickToCopyText(addressString);
                ImGui.PopStyleColor();
            } else {
                ClickToCopyText(addressString);
            }
        }

        public static void DrawUndockedPages() {
            string? closedWindow = null;
            foreach (var k in _plugin.PluginConfig.Debugging.Undocked) {
                if (debugPages.TryGetValue(k, out var debugPage)) {
                    ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.FirstUseEver);

                    var isOpen = true;

                    if (ImGui.Begin($"{_plugin.Name} - Debug : {k}", ref isOpen)) {
                        try {
                            debugPage();
                        } catch (Exception ex) {
                            SimpleLog.Error(ex);
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), ex.ToString());
                        }
                    }

                    ImGui.End();

                    if (!isOpen) closedWindow = k;
                }
            }

            if (!string.IsNullOrEmpty(closedWindow)) {
                _plugin.PluginConfig.Debugging.Undocked.Remove(closedWindow);
            }
        }
    }
}
