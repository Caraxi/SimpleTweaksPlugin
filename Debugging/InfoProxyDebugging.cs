
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class InfoProxyDebugging : DebugHelper {
    public override string Name => "Info Proxies";

    private InfoProxyId SelectedProxy;
    private Dictionary<InfoProxyId, Type> proxyType;

    public override void Draw() {
        if (proxyType == null) {
            proxyType = new Dictionary<InfoProxyId, Type>();
            foreach (var a in typeof(InfoProxyInterface).Assembly.GetTypes().Select((t) => (t, t.GetCustomAttributes(typeof(InfoProxyAttribute)).Cast<InfoProxyAttribute>().ToArray())).Where(t => t.Item2.Length > 0)) {
                foreach (var attr in a.Item2) {
                    proxyType.TryAdd(attr.ID, a.t);
                }
            }
        }

        var module = Framework.Instance()->GetUiModule()->GetInfoModule();
        if (module == null) return;

        DebugManager.PrintAddress(module);
        ImGui.SameLine();
        DebugManager.PrintOutObject(module);
        ImGui.Separator();

        if (ImGui.BeginChild("proxy_select", new Vector2(250 * ImGui.GetIO().FontGlobalScale, 0), true)) {
            for (var i = 0; i < 34; i++) {
                var p = (InfoProxyId)i;
                var n = $"{p}";
                if (n == $"{i}") n = $"InfoProxy#{i}";
                if (ImGui.Selectable($"{n}", SelectedProxy == p)) {
                    SelectedProxy = p;
                }
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("proxy_view", Vector2.Zero, true)) {
            var proxy = module->GetInfoProxyById(SelectedProxy);
            DebugManager.PrintAddress(proxy);
            if (proxyType.TryGetValue(SelectedProxy, out var type)) {
                
                var proxyObj = Marshal.PtrToStructure(new IntPtr(proxy), type);
                if (proxyObj != null) {
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(proxyObj, (ulong) proxy, autoExpand: true);
                }
            } else {
                ImGui.SameLine();
                DebugManager.PrintOutObject(proxy, autoExpand: true);
            }
        }
        ImGui.EndChild();
    }
}


