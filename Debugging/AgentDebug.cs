
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Interface.Style;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class AgentDebug : DebugHelper {
    private bool enableLogging = false;

    private delegate void* GetAgentByInternalIDDelegate(void* agentModule, AgentId agentId);

    private Hook<GetAgentByInternalIDDelegate> getAgentByInternalIdHook;
    private Hook<GetAgentByInternalIDDelegate> getAgentByInternalId2Hook;

    private List<(AgentId id, ulong address, ulong hitCount)> agentGetLog = new();
        
    public override void Draw() {

        if (ImGui.BeginTabBar("agenDebugTabs")) {

            if (ImGui.BeginTabItem("GetAgentByInternalID Calls")) {

                if (ImGui.Checkbox("Enable Logging", ref enableLogging)) {
                    if (enableLogging) {
                        SetupLogging();
                    } else {
                        DisableLogging();
                    }
                }

                ImGui.BeginChild($"scrolling", new Vector2(-1, -1));

                    
                ImGui.Columns(4);
                ImGui.Text($"ID");
                ImGui.NextColumn();
                ImGui.Text("Address");
                ImGui.NextColumn();
                ImGui.Text("VTable");
                ImGui.NextColumn();
                    
                ImGui.NextColumn();
                ImGui.Separator();
                ImGui.Separator();
                foreach (var l in agentGetLog) {
                        
                    ImGui.Text($"[{(uint)l.id}] {l.id}");
                    ImGui.NextColumn();
                    DebugManager.ClickToCopyText($"{l.address:X}");
                    ImGui.NextColumn();

                    var addr = (ulong*) (l.address);
                    if (addr != null) {
                        DebugManager.ClickToCopyText($"{addr[0]:X}");
                        var baseAddr = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress;
                        var offset = addr[0] - baseAddr;
                        ImGui.SameLine();
                        DebugManager.ClickToCopyText($"ffxiv_dx11.exe+{offset:X}");
                    }
                        
                    ImGui.NextColumn();
                    ImGui.Text($"{l.hitCount}");
                    ImGui.NextColumn();
                    ImGui.Separator();
                }


                ImGui.EndChild();
                    
                    
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Agents")) {

                if (ImGui.BeginTabBar("agentsTabs")) {

                    var agentClasses = typeof(AgentInterface).Assembly.GetTypes().Where(t => {
                        var attrs = t.GetCustomAttributes(typeof(AgentAttribute)).ToArray();
                        return attrs.Length > 0;
                    });

                    var i = 0;
                    foreach (var c in agentClasses) {
                        var name = c.Name;
                        if (c.Name.StartsWith("Agent")) name = c.Name.Substring(5);
                        if (ImGui.BeginTabItem($"{name}##{c.FullName}#{i++}")) {

                            var attr = (AgentAttribute) c.GetCustomAttributes(typeof(AgentAttribute)).First();
                            ImGui.Text($"{c.FullName}");
                            ImGui.Text("Instance:");
                            ImGui.SameLine();
                            var agentInstance = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(attr.ID);
                            DebugManager.ClickToCopyText($"{(ulong)agentInstance:X}");

                            if (agentInstance != null) {
                                ImGui.SameLine();
                                ImGui.Text("      VTable:");
                                ImGui.SameLine();
                                DebugManager.ClickToCopyText($"{(ulong)agentInstance->VTable:X}");

                                var beginModule = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                                var endModule = (beginModule + (ulong)Process.GetCurrentProcess().MainModule.ModuleMemorySize);
                                if (beginModule > 0 && (ulong)agentInstance->VTable >= beginModule && (ulong)agentInstance->VTable <= endModule) {
                                    ImGui.SameLine();
                                    ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                                    DebugManager.ClickToCopyText($"ffxiv_dx11.exe+{((ulong)agentInstance->VTable - beginModule):X}");
                                    ImGui.PopStyleColor();
                                }

                                ImGui.Separator();

                                ImGui.Text("Is Active:");
                                ImGui.SameLine();
                                var isActive = agentInstance->IsAgentActive();
                                ImGui.TextColored(isActive ? Colour.Green : Colour.Red, $"{isActive}");

                                ImGui.Separator();

                                var agentObj = Marshal.PtrToStructure(new IntPtr(agentInstance), c);
                                if (agentObj != null) {
                                    DebugManager.PrintOutObject(agentObj, (ulong) agentInstance);
                                }
                            }
                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void SetupLogging() {
        agentGetLog = new List<(AgentId, ulong, ulong)>();
        getAgentByInternalIdHook ??= new Hook<GetAgentByInternalIDDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 83 FF 0D"), new GetAgentByInternalIDDelegate(GetAgentByInternalIDDetour));
        getAgentByInternalId2Hook ??= new Hook<GetAgentByInternalIDDelegate>(Common.Scanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 12 0F BF 80"), new GetAgentByInternalIDDelegate(GetAgentByInternalIDDetour));
            
        getAgentByInternalIdHook?.Enable();
        getAgentByInternalId2Hook?.Enable();
    }

    private void* GetAgentByInternalIDDetour(void* agentModule, AgentId agentId) {
        var ret = getAgentByInternalIdHook.Original(agentModule, agentId);

        var e = agentGetLog.FirstOrDefault(a => a.id == agentId);
        var index = -1;
        if (e != default) index = agentGetLog.IndexOf(e);

        if (index < 0) {
            agentGetLog.Insert(0, (agentId, (ulong) ret, 1UL));
        } else {
            agentGetLog[index] = (agentId, (ulong) ret, e.hitCount + 1);
        }
            
        return ret;
    }

    private void DisableLogging() {
        getAgentByInternalIdHook?.Disable();
        getAgentByInternalId2Hook?.Disable();
    }

    public override void Dispose() {
        getAgentByInternalIdHook?.Disable();
        getAgentByInternalId2Hook?.Disable();
        getAgentByInternalIdHook?.Dispose();
        getAgentByInternalId2Hook?.Dispose();
        base.Dispose();
    }

    public override string Name => "Agent Debug";
}