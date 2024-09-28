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
using InteropGenerator.Runtime.Attributes;

namespace SimpleTweaksPlugin.Debugging;

public unsafe class InfoProxyDebugging : DebugHelper {
    public override string Name => "Info Proxies";

    private record InfoProxyInfo(Type Type, bool IsCommonList);

    private InfoProxyId SelectedProxy;

    private Dictionary<InfoProxyId, InfoProxyInfo> infoProxies;

    public override void Draw() {
        if (infoProxies == null) {
            infoProxies = new Dictionary<InfoProxyId, InfoProxyInfo>();
            foreach (var a in typeof(InfoProxyInterface).Assembly.GetTypes().Select((t) => (t, t.GetCustomAttributes(typeof(InfoProxyAttribute)).Cast<InfoProxyAttribute>().ToArray())).Where(t => t.Item2.Length > 0)) {
                foreach (var attr in a.Item2) {
                    infoProxies.TryAdd(attr.InfoProxyId, new InfoProxyInfo(a.t, a.t.GetCustomAttribute<InheritsAttribute<InfoProxyCommonList>>() != null));
                }
            }
        }

        var module = Framework.Instance()->GetUIModule()->GetInfoModule();
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
            if (infoProxies.TryGetValue(SelectedProxy, out var infoProxyInfo)) {
                var proxyObj = Marshal.PtrToStructure(new IntPtr(proxy), infoProxyInfo.Type);
                if (proxyObj != null) {
                    ImGui.SameLine();
                    DebugManager.PrintOutObject(proxyObj, (ulong)proxy, autoExpand: true);
                }

                if (infoProxyInfo.IsCommonList) {
                    var ipCommonList = (InfoProxyCommonList*)proxy;

                    ImGui.Text($"List Count: {ipCommonList->GetEntryCount()}");

                    for (var i = 0U; i < ipCommonList->GetEntryCount(); i++) {
                        var e = ipCommonList->GetEntry(i);
                        DebugManager.PrintOutObject(e);
                    }
                }
            } else {
                ImGui.SameLine();
                DebugManager.PrintOutObject(proxy, autoExpand: true);
            }
        }

        ImGui.EndChild();
    }
}
