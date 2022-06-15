using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Interface;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Debugging; 

public unsafe class AgentDebug : DebugHelper {
    private bool enableLogging = false;

    private delegate void* GetAgentByInternalIDDelegate(void* agentModule, AgentId agentId);

    private Hook<GetAgentByInternalIDDelegate> getAgentByInternalIdHook;
    private Hook<GetAgentByInternalIDDelegate> getAgentByInternalId2Hook;

    private List<(AgentId id, ulong address, ulong hitCount)> agentGetLog = new();

    private AgentId selectAgent;

    private List<(AgentId id, bool hasClass)> sortedAgentList;
    private float agentListWidth = 100f;
    private bool agentListActiveOnly = false;
    private bool agentListKnownOnly = true;
    private Type selectedAgentType;

    
    
    public class AgentEventHandlerHook : IDisposable {
        public AgentId AgentId { get; }
        private readonly AgentInterface* agentInterface;
        private readonly HookWrapper<AgentEventHandler> hook;
        
        public bool Disposed { get; private set; }

        public delegate void* AgentEventHandler(AgentInterface* agentInterface, void* a2, AtkValue* values, ulong atkValueCount, ulong eventType);

        public AgentEventHandlerHook(AgentId agentId) {
            AgentId = agentId;
            agentInterface = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(agentId);
            hook = Common.Hook<AgentEventHandler>(agentInterface->AtkEventInterface.vtbl[0], HandleEvent);
            hook?.Enable();
        }

        public void* HandleEvent(AgentInterface* agent, void* a2, AtkValue* values, ulong atkValueCount, ulong eventType) {
            if (Disposed || agent != agentInterface) return hook.Original(agent, a2, values, atkValueCount, eventType);

            try {
                var call = new EventCall() {
                    EventType = eventType,
                    UnknownPointer = a2,
                    UnknownPointerData = *(ulong*)a2, 
                };

                var v = values;
                for (var i = 0UL; i < atkValueCount; i++) {
                    call.AtkValueTypes.Add(v->Type);
                    call.AtkValues.Add((*v).ValueString());
                    v++;
                }
                
                EventCalls.Add(call);

                return hook.Original(agent, a2, values, atkValueCount, eventType);
            } catch (Exception ex) {
                //
            }
            
            
            return hook.Original(agent, a2, values, atkValueCount, eventType);
        }

        public class EventCall {
            public ulong EventType;
            public List<object> AtkValues = new();
            public List<ValueType> AtkValueTypes = new();
            public void* UnknownPointer;
            public ulong UnknownPointerData;
        } 
        
        public List<EventCall> EventCalls = new();


        public void Dispose() {
            if (Disposed) return;
            hook?.Disable();
            hook?.Dispose();
            Disposed = true;
        }
    }
    
    public Dictionary<AgentId, AgentEventHandlerHook> AgentEventHooks = new();
    
    public override void Draw() {
        if (sortedAgentList == null) {
            var maxAgentId = 0U;
            var l = new List<AgentId>();
            
            var agentClasses = typeof(AgentInterface).Assembly.GetTypes().Select((t) => (t, t.GetCustomAttributes(typeof(AgentAttribute)).Cast<AgentAttribute>().ToArray())).Where(t => t.Item2.Length > 0).ToArray();
            
            foreach (var a in Enum.GetValues(typeof(AgentId)).Cast<AgentId>()) {
                l.Add(a);
                if ((uint)a > maxAgentId) {
                    maxAgentId = (uint)a;
                }
            }

            sortedAgentList = l.Select((a) => {
                return (a, agentClasses.Any(t => t.Item2.Any(aa => aa.ID == a)));
            }).OrderBy(a => $"{a}").ToList();

            for (var i = 0U; i < maxAgentId; i++) {
                var a = (AgentId)i;
                if (sortedAgentList.Any(aa => a == aa.id)) continue;
                sortedAgentList.Add((a, false));
            }
            
        }

        
        
        if (ImGui.BeginTabBar("agenDebugTabs")) {
            if (ImGui.BeginTabItem("Agents")) {
                    
                if (ImGui.BeginChild("AgentList", new Vector2(agentListWidth, -1), true)) {

                    ImGui.Checkbox("Active Only", ref agentListActiveOnly);
                    ImGui.SameLine();
                    ImGui.Checkbox("Known Only", ref agentListKnownOnly);
                    ImGui.Separator();
                    if (ImGui.BeginChild("AgentListScroll", new Vector2(-1, -1), false)) {
                        foreach (var agent in sortedAgentList) {
                            var agentInterface = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(agent.id);
                            if (agentInterface == null) continue;
                            if (agentListKnownOnly && !agent.hasClass) continue;
                            var active = agentInterface->IsAgentActive();
                            if (agentListActiveOnly && !active) continue;
                            
                            ImGui.PushStyleColor(ImGuiCol.Text, agent.hasClass ? 0xFFFF5500: 0x000000);
                            ImGui.PushFont(UiBuilder.IconFont);
                            ImGui.Text($"{(char)FontAwesomeIcon.Atom}");
                            ImGui.PopFont();
                            ImGui.PopStyleColor();
                            ImGui.SameLine();
                            
                            ImGui.PushStyleColor(ImGuiCol.Text, active ? 0xFF00FF00 : 0xFF333388);
                            if (ImGui.Selectable($"{agent.id}", selectAgent == agent.id)) {
                                selectAgent = agent.id;
                                selectedAgentType = null;
                            }

                            var size = ImGui.CalcTextSize($"{agent}").X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ScrollbarSize * 2;
                            if (size > agentListWidth) {
                                agentListWidth = size;
                            }

                            ImGui.PopStyleColor();
                        }
                    }
                    ImGui.EndChild();
                    
                    

                }
                ImGui.EndChild();
                ImGui.SameLine();
                if (ImGui.BeginChild("AgentView", new Vector2(-1, -1), true)) {

                    var agentInterface = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(selectAgent);

                    var agentHook = AgentEventHooks.ContainsKey(selectAgent) ? AgentEventHooks[selectAgent] : null;

                    if (selectedAgentType == null) {
                        try {
                            var agentClasses = typeof(AgentInterface).Assembly.GetTypes().Select((t) => (t, t.GetCustomAttributes(typeof(AgentAttribute)).Cast<AgentAttribute>().ToArray())).Where(t => t.Item2.Length > 0).ToArray();
                            selectedAgentType = agentClasses.FirstOrDefault(t => t.Item2.Any(aa => aa.ID == selectAgent)).t;
                            selectedAgentType ??= typeof(AgentInterface);
                        } catch {
                            selectedAgentType = typeof(AgentInterface);
                        }
                    }

                    
                    
                    ImGui.Text($"{selectedAgentType.FullName}");
                    ImGui.Text("Instance:");
                    ImGui.SameLine();
                    DebugManager.ClickToCopyText($"{(ulong)agentInterface:X}");

                    if (agentInterface != null) {
                        ImGui.SameLine();
                        ImGui.Text("      VTable:");
                        ImGui.SameLine();
                        DebugManager.ClickToCopyText($"{(ulong)agentInterface->AtkEventInterface.vtbl:X}");

                        var beginModule = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                        var endModule = (beginModule + (ulong)Process.GetCurrentProcess().MainModule.ModuleMemorySize);
                        if (beginModule > 0 && (ulong)agentInterface->AtkEventInterface.vtbl >= beginModule && (ulong)agentInterface->AtkEventInterface.vtbl <= endModule) {
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                            DebugManager.ClickToCopyText($"ffxiv_dx11.exe+{((ulong)agentInterface->AtkEventInterface.vtbl - beginModule):X}");
                            ImGui.PopStyleColor();
                        }

                        ImGui.Separator();
                        
                        ImGui.Text("Event Log:");
                        ImGui.SameLine();
                        if (agentHook == null || agentHook.Disposed) {
                            if (ImGui.Button("Hook Events")) {
                                if (agentHook != null) AgentEventHooks.Remove(selectAgent);
                                AgentEventHooks.Add(selectAgent, new AgentEventHandlerHook(selectAgent));
                            }
                        } else {
                            if (ImGui.Button("Disable Hook")) {
                                agentHook.Dispose();
                            }
                        }

                        if (agentHook != null) {
                            if (ImGui.TreeNode($"Event Calls({agentHook.EventCalls.Count})###eventCalls_{selectAgent}")) {

                                foreach (var call in agentHook.EventCalls) {
                                    
                                    ImGui.Text($"Event#{call.EventType} - {call.AtkValueTypes.Count} Values");
                                    ImGui.SameLine();
                                    DebugManager.ClickToCopy(call.UnknownPointer);
                                    ImGui.SameLine();
                                    DebugManager.ClickToCopyText($"[{call.UnknownPointerData:X}]", $"{call.UnknownPointerData:X}");
                                    ImGui.Indent();

                                    for (var i = 0; i < call.AtkValueTypes.Count && i < call.AtkValues.Count; i++) {
                                        ImGui.Text($"[#{i}] {call.AtkValueTypes[i]} : {call.AtkValues[i]}");
                                    }

                                    ImGui.Unindent();
                                    
                                    
                                }
                                
                                
                                ImGui.TreePop();
                            }
                        }
                        
                        
                        
                        
                        
                        
                        ImGui.Separator();

                        ImGui.Text("Is Active:");
                        ImGui.SameLine();
                        var isActive = agentInterface->IsAgentActive();
                        ImGui.TextColored(isActive ? Colour.Green : Colour.Red, $"{isActive}");


                        ImGui.Separator();

                        var agentObj = Marshal.PtrToStructure(new IntPtr(agentInterface), selectedAgentType);
                        if (agentObj != null) {
                            DebugManager.PrintOutObject(agentObj, (ulong) agentInterface);
                        }
                        
                        ImGui.Separator();
                        ImGui.Separator();
                        
                        ImGui.Text($"Addon: ");
                        
                        var addonId = agentInterface->GetAddonID();
                        AtkUnitBase* addon;
                        if (addonId == 0 || (addon = Common.GetAddonByID(addonId)) == null ) {
                            ImGui.SameLine();
                            ImGui.Text("None");
                        } else {
                            UIDebug.DrawUnitBase(addon);
                        }
                    }
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            
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
            
            ImGui.EndTabBar();
        }
    }

    private void SetupLogging() {
        agentGetLog = new List<(AgentId, ulong, ulong)>();
        getAgentByInternalIdHook ??= new Hook<GetAgentByInternalIDDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 FF 0D"), new GetAgentByInternalIDDelegate(GetAgentByInternalIDDetour));
        getAgentByInternalId2Hook ??= new Hook<GetAgentByInternalIDDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 12 0F BF 80"), new GetAgentByInternalIDDelegate(GetAgentByInternalIDDetour));
            
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

        foreach (var h in AgentEventHooks.Values) {
            h.Dispose();
        }
        AgentEventHooks.Clear();
        
        base.Dispose();
    }

    public override string Name => "Agent Debug";
}