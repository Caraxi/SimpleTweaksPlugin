using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using ImGuiScene;
using SimpleTweaksPlugin.Utility;
using Action = System.Action;
using Addon = FFXIVClientStructs.Attributes.Addon;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

#pragma warning disable 659

// Customised version of https://github.com/aers/FFXIVUIDebug


namespace SimpleTweaksPlugin.Debugging; 

public partial class DebugConfig {
    public ulong SelectedAtkUnitBase;
    public string AtkUnitBaseSearch = string.Empty;
}
    
    
public unsafe class UIDebug : DebugHelper {
    private bool firstDraw = true;
    private bool elementSelectorActive = false;
    private int elementSelectorIndex = 0;
    private static float elementSelectorCountdown = 0;
    private static bool elementSelectorScrolled = false;
    private static int loadImageId = 0;
    private static int loadImageVersion = 0;
    private static ulong[] elementSelectorFind = {};
    private AtkUnitBase* selectedUnitBase = null;
    private string addressSearchInput = string.Empty;

    public const int UnitListCount = 18;
    private readonly bool[] selectedInList = new bool[UnitListCount];
    private readonly string[] listNames = new string[UnitListCount]{
        "Depth Layer 1",
        "Depth Layer 2",
        "Depth Layer 3",
        "Depth Layer 4",
        "Depth Layer 5",
        "Depth Layer 6",
        "Depth Layer 7",
        "Depth Layer 8",
        "Depth Layer 9",
        "Depth Layer 10",
        "Depth Layer 11",
        "Depth Layer 12",
        "Depth Layer 13",
        "Loaded Units",
        "Focused Units",
        "Units 16",
        "Units 17",
        "Units 18"
    };

    private static RawDX11Scene.BuildUIDelegate originalHandler;


    internal bool FindByAddress(AtkResNode* node, ulong address, out List<ulong>? path) {
        if (node == null) {
            path = null;
            return false;
        }

        if ((ulong)node == address) {
            path = new List<ulong>() { (ulong) node };
            return true;
        }

        if ((int)node->Type >= 1000) {
            var cNode = (AtkComponentNode*)node;

            if (cNode->Component != null) {
                if ((ulong)cNode->Component == address) {
                    path = new List<ulong>() { (ulong) node };
                    return true;
                }
                
                if (FindByAddress(cNode->Component->UldManager.RootNode, address, out path) && path != null) {
                    path.Add((ulong)node);
                    return true;
                }
            }
        }

        if (FindByAddress(node->ChildNode, address, out path) && path != null) {
            path.Add((ulong)node);
            return true;
        }

        if (FindByAddress(node->PrevSiblingNode, address, out path) && path != null) {
            return true;
        }
        
        path = null;
        return false;
    }
    
    internal bool FindByAddress(AtkUnitBase* atkUnitBase, ulong address) {
        if (atkUnitBase->RootNode == null) return false;
        if (FindByAddress(atkUnitBase->RootNode, address, out var path)) {
            elementSelectorScrolled = false;
            elementSelectorFind = path.ToArray();
            elementSelectorCountdown = 100;
            return true;
        }
        return false;
    }

    internal void FindByAddress(ulong address) {
        var stage = AtkStage.GetSingleton();
                
        var unitManagers = &stage->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        
        for (var i = 0; i < UnitListCount; i++) {
            var unitManager = &unitManagers[i];
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->EntriesSpan.Length))) {
                var unitBase = unitManager->EntriesSpan[j].Value;
                if ((ulong)unitBase == address || FindByAddress(unitBase, address)) {
                    selectedUnitBase = unitBase;
                    Plugin.PluginConfig.Debugging.SelectedAtkUnitBase = address;
                    Plugin.PluginConfig.Save();
                    return;
                }
            }
        }
    }

    internal static bool SetExclusiveDraw(Action action) {
        // Possibly the most cursed shit I've ever done.
        if (originalHandler != null) return false;
        try {
            var dalamudAssembly = Service.PluginInterface.GetType().Assembly;
            var service1T = dalamudAssembly.GetType("Dalamud.Service`1");
            var interfaceManagerT = dalamudAssembly.GetType("Dalamud.Interface.Internal.InterfaceManager");
            if (service1T == null) return false;
            if (interfaceManagerT == null) return false;
            var serviceInterfaceManager = service1T.MakeGenericType(interfaceManagerT);
            var getter = serviceInterfaceManager.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getter == null) return false;
            var interfaceManager = getter.Invoke(null, null);
            if (interfaceManager == null) return false;
            var ef = interfaceManagerT.GetField("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ef == null) return false;
            var handler = (RawDX11Scene.BuildUIDelegate) ef.GetValue(interfaceManager);
            if (handler == null) return false;
            originalHandler = handler;
            ef.SetValue(interfaceManager, new RawDX11Scene.BuildUIDelegate(action));
            return true;
        } catch (Exception ex) {
            SimpleLog.Fatal(ex);
            SimpleLog.Fatal("This could be messy...");
        }
        return false;
    }
        
    internal static bool FreeExclusiveDraw() {
        if (originalHandler == null) return true;
        try {
            var dalamudAssembly = Service.PluginInterface.GetType().Assembly;
            var service1T = dalamudAssembly.GetType("Dalamud.Service`1");
            var interfaceManagerT = dalamudAssembly.GetType("Dalamud.Interface.Internal.InterfaceManager");
            if (service1T == null) return false;
            if (interfaceManagerT == null) return false;
            var serviceInterfaceManager = service1T.MakeGenericType(interfaceManagerT);
            var getter = serviceInterfaceManager.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getter == null) return false;
            var interfaceManager = getter.Invoke(null, null);
            if (interfaceManager == null) return false;
            var ef = interfaceManagerT.GetField("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ef == null) return false;
            ef.SetValue(interfaceManager, originalHandler);
            originalHandler = null;
            return true;
        } catch (Exception ex) {
            SimpleLog.Fatal(ex);
            SimpleLog.Fatal("This could be messy...");
        }

        return false;
    }
        
    public override void Draw() {
            
        if (firstDraw) {
            firstDraw = false;
            selectedUnitBase = (AtkUnitBase*) (Plugin.PluginConfig.Debugging.SelectedAtkUnitBase);
        }

        ImGui.BeginGroup();
        ImGui.BeginChild("st_uiDebug_unitBaseSelect", new Vector2(250, -44), true);
            
        ImGui.SetNextItemWidth(-38);
        ImGui.InputTextWithHint("###atkUnitBaseSearch", "Search", ref Plugin.PluginConfig.Debugging.AtkUnitBaseSearch, 0x20);
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, elementSelectorActive ? 0xFF00FFFF : 0xFFFFFFFF);
        if (ImGui.Button($"{(char) FontAwesomeIcon.ObjectUngroup}", new Vector2(-1, ImGui.GetItemRectSize().Y))) {
            elementSelectorActive = !elementSelectorActive;
            Service.PluginInterface.UiBuilder.Draw -= DrawElementSelector;
            FreeExclusiveDraw();
                
            if (elementSelectorActive) {
                SetExclusiveDraw(DrawElementSelector);
            }
        }
        ImGui.PopStyleColor();
        ImGui.PopFont();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Element Selector");
            
        DrawUnitBaseList();
        ImGui.EndChild();
        if (ImGui.BeginChild("###uiDebug_addressSearch", new Vector2(250, 0), true)) {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 32);
            ImGui.InputTextWithHint("###addressSearchInput", "Address Search", ref addressSearchInput, 18, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Search)) {
                if (ulong.TryParse(addressSearchInput, NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out var address)) {
                    FindByAddress(address);
                }
            }
        }
        ImGui.EndChild();
        ImGui.EndGroup();
        if (selectedUnitBase != null) {
            ImGui.SameLine();
            ImGui.BeginChild("st_uiDebug_selectedUnitBase", new Vector2(-1, -1), true);
            DrawUnitBase(selectedUnitBase);
            ImGui.EndChild();
        }

        if (elementSelectorCountdown > 0) {
            elementSelectorCountdown -= 1;
            if (elementSelectorCountdown < 0) elementSelectorCountdown = 0;
        }

    }

    public override void Dispose() {
        FreeExclusiveDraw();
    }

    private void DrawElementSelector() {
        ImGui.GetIO().WantCaptureKeyboard = true;
        ImGui.GetIO().WantCaptureMouse = true;
        ImGui.GetIO().WantTextInput = true;
        if (ImGui.IsKeyPressed(ImGuiKey.Escape)) {
            elementSelectorActive = false;
            FreeExclusiveDraw();
            return;
        }
            
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.SetNextWindowBgAlpha(0.3f);
        ImGuiHelpers.ForceNextWindowMainViewport();
        
        ImGui.Begin("ElementSelectorWindow", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar);
        var drawList = ImGui.GetWindowDrawList();
            
        var y = 100f;
        foreach (var s in new[]{"Select an Element", "Press ESCAPE to cancel"}) {
            var size = ImGui.CalcTextSize(s);
            var x = ImGuiExt.GetWindowContentRegionSize().X / 2f - size.X / 2;
            drawList.AddText(new Vector2(x, y), 0xFFFFFFFF, s);
            
            y += size.Y;
        }
            
        var mousePos = ImGui.GetMousePos() - ImGuiHelpers.MainViewport.Pos;
        var windows = GetAtkUnitBaseAtPosition(mousePos);

        ImGui.SetCursorPosX(100);
        ImGui.SetCursorPosY(100);
        ImGui.BeginChild("noClick", new Vector2(800, 2000), false, ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.BeginGroup();
            
        ImGui.Text($"Mouse Position: {mousePos.X}, {mousePos.Y}\n");
        var i = 0;
            
        foreach (var a in windows) {
            var name = Marshal.PtrToStringAnsi(new IntPtr(a.UnitBase->Name));
            ImGui.Text($"[Addon] {name}");
            ImGui.Indent(15);
            foreach (var n in a.Nodes) {
                var nSelected = i++ == elementSelectorIndex;
                if (nSelected) ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FFFF);
                // ImGui.Text($"{((int)n.ResNode->Type >= 1000 ? ((ULDComponentInfo*)((AtkComponentNode*) n.ResNode)->Component->UldManager.Objects)->ComponentType.ToString() + "ComponentNode" : n.ResNode->Type.ToString() + "Node")}");
                    
                PrintNode(n.ResNode, false, null, true);
                    
                    
                if (nSelected) ImGui.PopStyleColor();

                if (nSelected && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                    elementSelectorActive = false;
                    FreeExclusiveDraw();

                    selectedUnitBase = a.UnitBase;

                    var l = new List<ulong>();
                        
                    l.Add((ulong)n.ResNode);
                    var nextNode = n.ResNode->ParentNode;
                    while (nextNode != null) {
                        l.Add((ulong) nextNode);
                        nextNode = nextNode->ParentNode;
                    }

                    elementSelectorFind = l.ToArray();
                    elementSelectorCountdown = 100;
                    elementSelectorScrolled = false;
                }
                    
                    
                drawList.AddRectFilled(n.State.Position + ImGuiHelpers.MainViewport.Pos, n.State.SecondPosition + ImGuiHelpers.MainViewport.Pos, (uint) (nSelected ? 0x4400FFFF: 0x0000FF00));
            }
            ImGui.Indent(-15);
        }

        if (i != 0) {
            elementSelectorIndex -= (int) ImGui.GetIO().MouseWheel;
            while (elementSelectorIndex < 0) elementSelectorIndex += i;
            while (elementSelectorIndex >= i) elementSelectorIndex -= i;
        }

        ImGui.EndGroup();
        ImGui.EndChild();
        ImGui.End();
    }

    private IEnumerable<AddonResult> GetAtkUnitBaseAtPosition(Vector2 position) {
        var list = new List<AddonResult>();
        var stage = AtkStage.GetSingleton();
        var unitManagers = &stage->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        for (var i = 0; i < UnitListCount; i++) {
            var unitManager = &unitManagers[i];
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->EntriesSpan.Length))) {
                var unitBase = unitManager->EntriesSpan[j].Value;
                if (unitBase == null || unitBase->RootNode == null) continue;
                if (!(unitBase->IsVisible && unitBase->RootNode->IsVisible)) continue;
                var addonResult = new AddonResult() {UnitBase = unitBase};
                if (list.Contains(addonResult)) continue;
                if (unitBase->X > position.X || unitBase->Y > position.Y) continue;
                if (unitBase->X + unitBase->RootNode->Width < position.X) continue;
                if (unitBase->Y + unitBase->RootNode->Height < position.Y) continue;
                addonResult.Nodes = GetAtkResNodeAtPosition(unitBase->UldManager, position);
                list.Add(addonResult);
            }
        }

        return list.OrderBy(w => w.UnitBase->GetScaledWidth(true) * w.UnitBase->GetScaledHeight(true));
    }

    private class AddonResult {
        public AtkUnitBase* UnitBase;
        public List<NodeResult> Nodes = new();
            
        public override bool Equals(object obj) {
            if (!(obj is AddonResult ar)) return false;
            return UnitBase == ar.UnitBase;
        }
    }
        
    private class NodeResult {
        public AtkResNode* ResNode;
        public NodeState State;
        public override bool Equals(object obj) {
            if (!(obj is NodeResult nr)) return false;
            return nr.ResNode == ResNode;
        }
    }

    private static Dictionary<string, Type> addonMapping = new Dictionary<string, Type>();
        
        
    private List<NodeResult> GetAtkResNodeAtPosition(AtkUldManager UldManager, Vector2 position, bool noReverse = false) {
        var list = new List<NodeResult>();
        for (var i = 0; i < UldManager.NodeListCount; i++) {
            var node = UldManager.NodeList[i];
            var state = GetNodeState(node);
            if (state.Visible) {
                if (state.Position.X > position.X) continue;
                if (state.Position.Y > position.Y) continue;
                if (state.SecondPosition.X < position.X) continue;
                if (state.SecondPosition.Y < position.Y) continue;
                    

                if ((int) node->Type >= 1000) {
                    var compNode = (AtkComponentNode*) node;
                    list.AddRange(GetAtkResNodeAtPosition(compNode->Component->UldManager, position, true));
                }
                    
                list.Add(new NodeResult() {
                    State = state,
                    ResNode = node,
                });
            }
        }

        list.Reverse();
        return list;
    }
        
    public static void DrawUnitBase(AtkUnitBase* atkUnitBase) {

        var isVisible = (atkUnitBase->Flags & 0x20) == 0x20;
        var addonName = Marshal.PtrToStringAnsi(new IntPtr(atkUnitBase->Name));
            
        ImGui.Text($"{addonName}");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, isVisible ? 0xFF00FF00 : 0xFF0000FF);
        ImGui.Text(isVisible ? "Visible" : "Not Visible");
        ImGui.PopStyleColor();

        ImGui.SameLine();

        if (!AddonDebug.IsSetupHooked(atkUnitBase)) {
            if (ImGui.SmallButton("Hook Setup")) {
                AddonDebug.HookOnSetup(atkUnitBase);
            }
        }


        ImGui.SameLine(ImGuiExt.GetWindowContentRegionSize().X - 25);
        if (ImGui.SmallButton("V")) {
            if (atkUnitBase->IsVisible) {
                atkUnitBase->Flags ^= 0x20;
            } else {
                atkUnitBase->Show(false, 0);
            }
        }
            
        ImGui.Separator();
        DebugManager.ClickToCopyText($"Address: {(ulong) atkUnitBase:X}", $"{(ulong) atkUnitBase:X}");
        ImGui.Separator();
            
#if DEBUG
        var position = new Vector2(atkUnitBase->X, atkUnitBase->Y);
        ImGui.PushItemWidth(180);
        if (ImGui.SliderFloat($"##xPos_atkUnitBase#{(ulong) atkUnitBase:X}", ref position.X, position.X - 10, position.X + 10)) {
            UiHelper.SetPosition(atkUnitBase, position.X, position.Y);
            UiHelper.SetPosition(atkUnitBase->RootNode, position.X, position.Y);
        }
        ImGui.SameLine();
        if (ImGui.SliderFloat($"Position##yPos_atkUnitBase#{(ulong) atkUnitBase:X}", ref position.Y, position.Y - 10, position.Y + 10)) {
            UiHelper.SetPosition(atkUnitBase, position.X, position.Y);
            UiHelper.SetPosition(atkUnitBase->RootNode, position.X, position.Y);
        }
        ImGui.PopItemWidth();
#else
            ImGui.Text($"Position: [ {atkUnitBase->X} , {atkUnitBase->Y} ]");
#endif
        ImGui.Text($"Scale: {atkUnitBase->Scale*100}%%");
        ImGui.Text($"Widget Count {atkUnitBase->UldManager.ObjectCount}");
        var atkValue = atkUnitBase->AtkValues;
        if (atkUnitBase->AtkValuesCount > 0 && atkValue != null) {
            
            if (ImGui.TreeNode($"Atk Values ({atkUnitBase->AtkValuesCount})###atkValues_atkUnitBase")) {

                if (ImGui.BeginTable("atkUnitBase_atkValueTable", 3)) {
                    
                    ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 50 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120 * ImGui.GetIO().FontGlobalScale);
                    ImGui.TableSetupColumn("Value");
                    ImGui.TableHeadersRow();
                    
                    try {
                        
                        for (var i = 0; i < atkUnitBase->AtkValuesCount; i++) {
                            ImGui.TableNextColumn();
                            if (atkValue->Type == 0) {
                                ImGui.TextDisabled($"#{i}");
                            } else {
                                ImGui.Text($"#{i}");
                            }

                            ImGui.TableNextColumn();
                            if (atkValue->Type == 0) {
                                ImGui.TextDisabled("Not Set");
                            } else {
                                ImGui.Text($"{atkValue->Type}");
                            }
                            
                            ImGui.TableNextColumn();
                            
                            switch (atkValue->Type) {
                                case 0:
                                    break;
                                case ValueType.Int: {
                                    ImGui.Text($"{atkValue->Int}");
                                    break;
                                }
                                case ValueType.AllocatedString:
                                case ValueType.String8:
                                case ValueType.String: {
                                    if (atkValue->String == null) {
                                        ImGui.TextDisabled("null");
                                    } else {
                                        var str = MemoryHelper.ReadSeStringNullTerminated(new nint(atkValue->String));
                                        DebugManager.PrintOutObject(str, (ulong) atkValue);
                                    }
                                    break;
                                }
                                case ValueType.UInt: {
                                    ImGui.Text($"{atkValue->Int}");
                                    break;
                                }
                                case ValueType.Bool: {
                                    ImGui.Text($"{atkValue->Byte != 0}");
                                    break;
                                }
                                case ValueType.Texture: {
                                    DebugManager.PrintOutObject(atkValue->Texture);
                                    break;
                                }
                                default: {
                                    ImGui.TextDisabled("Unhandled Type");
                                    ImGui.SameLine();
                                    DebugManager.PrintOutObject(atkValue);
                                    break;
                                }
                            }
                            
                            atkValue++;
                        }
                        
                    } catch (Exception ex) {
                        ImGui.TextColored(ImGuiColors.DalamudRed, $"{ex}");
                    }
                    ImGui.EndTable();
                }

                ImGui.TreePop();
            }
        }
            
        ImGui.Separator();

        object addonObj;

        if (addonName != null && addonMapping.ContainsKey(addonName)) {
            if (addonMapping[addonName] == null) {
                addonObj = *atkUnitBase;
            } else {
                addonObj = Marshal.PtrToStructure(new IntPtr(atkUnitBase), addonMapping[addonName]);
            }
                
        } else if (addonName != null) {

            addonMapping.Add(addonName, null);

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    foreach (var t in a.GetTypes()) {
                        if (!t.IsPublic) continue;
                        var xivAddonAttr = (Addon) t.GetCustomAttribute(typeof(Addon), false);
                        if (xivAddonAttr == null) continue;
                        if (!xivAddonAttr.AddonIdentifiers.Contains(addonName)) continue;
                        addonMapping[addonName] = t;
                        break;
                    }
                }
                catch {
                    // ignored
                }
            }
                
            addonObj = *atkUnitBase;
        } else {
            addonObj = *atkUnitBase;
        }
            
        DebugManager.PrintOutObject(addonObj, (ulong) atkUnitBase, new List<string>());

        ImGui.Dummy(new Vector2(25 * ImGui.GetIO().FontGlobalScale));
        ImGui.Separator();

        if (atkUnitBase->RootNode != null)
            PrintNode(atkUnitBase->RootNode);

            
        if (atkUnitBase->UldManager.NodeListCount > 0) {
            ImGui.Dummy(new Vector2(25 * ImGui.GetIO().FontGlobalScale));
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFAAAA);
            if (ImGui.TreeNode($"Node List##{(ulong)atkUnitBase:X}")) {
                ImGui.PopStyleColor();

                for (var j = 0; j < atkUnitBase->UldManager.NodeListCount; j++) {
                    PrintNode(atkUnitBase->UldManager.NodeList[j], false, $"[{j}] ");
                }
                ImGui.TreePop();
            } else {
                ImGui.PopStyleColor();
            }
        }

        if (atkUnitBase->CollisionNodeListCount > 0) {
            ImGui.Dummy(new Vector2(25 * ImGui.GetIO().FontGlobalScale));
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFAAAA);
            if (ImGui.TreeNode($"Collision List##{(ulong)atkUnitBase:X}")) {
                ImGui.PopStyleColor();

                for (var j = 0; j < atkUnitBase->CollisionNodeListCount; j++) {
                    PrintNode((AtkResNode*) atkUnitBase->CollisionNodeList[j], false, $"[{j}] ");
                }
                ImGui.TreePop();
            } else {
                ImGui.PopStyleColor();
            }
        }

        if (elementSelectorFind.Length > 0 && elementSelectorCountdown <= 0) {
            elementSelectorFind = new ulong[0];
        }
            
    }

        
    public static void PrintNode(AtkResNode* node, bool printSiblings = true, string treePrefix = "", bool textOnly = false)
    {
        if (node == null)
            return;

        var aPos = ImGui.GetCursorScreenPos();
        if (elementSelectorFind.Length > 0 && elementSelectorFind[0] == (ulong) node && !elementSelectorScrolled) {
            ImGui.SetScrollHereY();
            elementSelectorScrolled = true;
        }
            
        if ((int)node->Type < 1000)
            PrintSimpleNode(node, treePrefix, textOnly);
        else
            PrintComponentNode(node, treePrefix, textOnly);
            
        if (elementSelectorFind.Length > 0 && elementSelectorFind[0] == (ulong) node) {
            var bPos = ImGui.GetCursorScreenPos();

            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(aPos - new Vector2(5), bPos + new Vector2(ImGui.GetWindowWidth(), 5), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.5f * (elementSelectorCountdown / 100))));
        }
            
        if (printSiblings)
        {
            var prevNode = node;
            while ((prevNode = prevNode->PrevSiblingNode) != null)
                PrintNode(prevNode, false, "prev ");

            var nextNode = node;
            while ((nextNode = nextNode->NextSiblingNode) != null)
                PrintNode(nextNode, false, "next ");
        }
    }

    private static Dictionary<uint, string> customNodeIds;

    private static string NodeID(uint id, bool includeHashOnNoMatch = true) {
        if (customNodeIds == null) {
            customNodeIds = new Dictionary<uint, string>();
            foreach (var f in typeof(CustomNodes).GetFields(BindingFlags.Static | BindingFlags.Public)) {
                if (f.FieldType == typeof(int)) {
                    var v = (int?) f.GetValue(null);
                    if (v == null || customNodeIds.ContainsKey((uint)v.Value)) continue;
                    customNodeIds.Add((uint) v.Value, f.Name);
                }
            }
        }
        
        if (!CustomNodes.TryGet(id, out var customNodeName)) {
            customNodeName = customNodeIds.ContainsKey(id) ? customNodeIds[id] : string.Empty;
        }
        
        return string.IsNullOrEmpty(customNodeName) ? (includeHashOnNoMatch ? $"#{id}" : $"{id}") : $"{customNodeName}#{id}";
    }

    private static int counterNodeInputNumber = 0;
    
    private static void PrintSimpleNode(AtkResNode* node, string treePrefix, bool textOnly = false)
    {
        bool popped = false;
        bool isVisible = node->IsVisible;

        if (isVisible && !textOnly)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 255, 0, 255));

        if (elementSelectorFind.Length > 0) {
            ImGui.SetNextItemOpen(elementSelectorFind.Contains((ulong) node), ImGuiCond.Always);
        }
        if (textOnly) ImGui.SetNextItemOpen(false, ImGuiCond.Always);
        if (ImGui.TreeNode($"{treePrefix} [{NodeID(node->NodeID)}] {node->Type} Node (ptr = {(long)node:X})###{(long)node}"))
        {
            if (ImGui.IsItemHovered()) DrawOutline(node);
            if (isVisible && !textOnly)
            {
                ImGui.PopStyleColor();
                popped = true;
            }

            ImGui.Text("Node: ");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)node:X}");
            ImGui.SameLine();
            switch (node->Type) {
                case NodeType.Text: DebugManager.PrintOutObject(*(AtkTextNode*)node, (ulong) node, new List<string>()); break;
                case NodeType.Image: DebugManager.PrintOutObject(*(AtkImageNode*)node, (ulong) node, new List<string>()); break;
                case NodeType.Collision: DebugManager.PrintOutObject(*(AtkCollisionNode*)node, (ulong) node, new List<string>()); break;
                case NodeType.NineGrid: DebugManager.PrintOutObject(*(AtkNineGridNode*)node, (ulong) node, new List<string>()); break;
                case NodeType.Counter: DebugManager.PrintOutObject(*(AtkCounterNode*)node, (ulong) node, new List<string>()); break;
                default: DebugManager.PrintOutObject(*node, (ulong) node, new List<string>()); break;
            }
                
            PrintResNode(node);

            if (node->ChildNode != null)
            {
                PrintNode(node->ChildNode);
            }

            switch (node->Type)
            {
                case NodeType.Text:
                    var textNode = (AtkTextNode*)node;
                    ImGui.Text($"text: {Marshal.PtrToStringAnsi(new IntPtr(textNode->NodeText.StringPtr))}");
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FFFF);
                    if (ImGui.TreeNode($"Payloads##{(ulong) textNode:X}")) {
                        ImGui.PopStyleColor();
                        var seStringBytes = new byte[textNode->NodeText.BufUsed];
                        for (var i = 0L; i < textNode->NodeText.BufUsed; i++) {
                            seStringBytes[i] = textNode->NodeText.StringPtr[i];
                        }
                        var seString = SeString.Parse(seStringBytes);
                        for (var i = 0; i < seString.Payloads.Count; i++) {
                            var payload = seString.Payloads[i];
                            ImGui.Text($"[{i}]");
                            ImGui.SameLine();
                            switch (payload.Type) {
                                case PayloadType.RawText when payload is TextPayload tp: {
                                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                                    ImGui.Text("Raw Text: '");
                                    ImGui.SameLine();
                                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFFAAAAAA);
                                    ImGui.Text(tp.Text);
                                    ImGui.PopStyleColor();
                                    ImGui.SameLine();
                                    ImGui.PopStyleVar();
                                    ImGui.Text("'");
                                    break;
                                }
                                default: {
                                    ImGui.Text(payload.ToString());
                                    break;
                                }
                            }
                        }
                            
                        ImGui.TreePop();
                    } else {
                        ImGui.PopStyleColor();
                    }

                    var s = textNode->NodeText.ToString();

                    if (ImGui.InputText($"Replace Text##{(ulong)textNode:X}", ref s, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
                        textNode->SetText(s);
                    }


                    ImGui.Text($"AlignmentType: {(AlignmentType)textNode->AlignmentFontType}[{textNode->AlignmentFontType:X2}]  FontSize: {textNode->FontSize}  CharSpacing: {textNode->CharSpacing}  LineSpacing: {textNode->LineSpacing}");
                    int b = textNode->AlignmentFontType;
                    if (ImGui.InputInt($"###setAlignment{(ulong) textNode:X}", ref b, 1)) {
                        while (b > byte.MaxValue) b -= byte.MaxValue;
                        while (b < byte.MinValue) b += byte.MaxValue;
                        textNode->AlignmentFontType = (byte) b;
                        textNode->AtkResNode.DrawFlags |= 0x1;
                    }
                        
                    ImGui.Text($"Color: #{textNode->TextColor.R:X2}{textNode->TextColor.G:X2}{textNode->TextColor.B:X2}{textNode->TextColor.A:X2}");
                    ImGui.SameLine();
                    ImGui.Text($"EdgeColor: #{textNode->EdgeColor.R:X2}{textNode->EdgeColor.G:X2}{textNode->EdgeColor.B:X2}{textNode->EdgeColor.A:X2}");
                    ImGui.SameLine();
                    ImGui.Text($"BGColor: #{textNode->BackgroundColor.R:X2}{textNode->BackgroundColor.G:X2}{textNode->BackgroundColor.B:X2}{textNode->BackgroundColor.A:X2}");

                    ImGui.Text($"TextFlags: {textNode->TextFlags}");
                    ImGui.SameLine();
                    ImGui.Text($"TextFlags2: {textNode->TextFlags2}");



                    break;
                case NodeType.Counter:
                    var counterNode = (AtkCounterNode*)node;

                    var text = counterNode->NodeText.ToString();
                    ImGui.Text($"text: {text}");

                    if (ImGui.InputText($"Replace Text##{(ulong)counterNode:X}", ref text, 100, ImGuiInputTextFlags.EnterReturnsTrue)) {
                        counterNode->SetText(text);
                    }

                    ImGui.InputInt($"##input_{(ulong)counterNode:X}", ref counterNodeInputNumber);
                    ImGui.SameLine();
                    if (ImGui.Button($"Set##{(ulong)counterNode:X}")) {
                        counterNode->SetNumber(counterNodeInputNumber);
                    }
                    
                    
                    
                    break;
                case NodeType.NineGrid:
                case NodeType.Image:
                    var iNode = (AtkImageNode*)node;
                    ImGui.Text($"wrap: {iNode->WrapMode}, flags: {iNode->Flags}");
                    if (iNode->PartsList != null) {
                        if (iNode->PartId > iNode->PartsList->PartCount) {
                            ImGui.Text($"part id({iNode->PartId}) > part count({iNode->PartsList->PartCount})?");
                        } else {
                            var part = iNode->PartsList->Parts[iNode->PartId];
                            var textureInfo = part.UldAsset;
                            var texType = textureInfo->AtkTexture.TextureType;
                            ImGui.Text($"texture type: {texType} part_id={iNode->PartId} part_id_count={iNode->PartsList->PartCount}");
                            if (texType == TextureType.Resource) {
                                var texFileNamePtr = textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                                var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr.BufferPtr));
                                var isHighResolution = texString?.Contains("_hr1") ?? false;
                                ImGui.Text($"texture path: {texString}");
                                var kernelTexture = textureInfo->AtkTexture.Resource->KernelTextureObject;

                                if (ImGui.TreeNode($"Texture##{(ulong) kernelTexture->D3D11ShaderResourceView:X}")) {
                                    var textureSize = new Vector2(kernelTexture->Width, kernelTexture->Height);
                                    ImGui.Image(new IntPtr(kernelTexture->D3D11ShaderResourceView), new Vector2(kernelTexture->Width, kernelTexture->Height));

                                    if (ImGui.TreeNode($"Parts##{(ulong)kernelTexture->D3D11ShaderResourceView:X}")) {

                                        ImGui.BeginTable($"partsTable##{(ulong)kernelTexture->D3D11ShaderResourceView:X}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
                                        ImGui.TableSetupColumn("Part ID", ImGuiTableColumnFlags.WidthFixed, 80);
                                        ImGui.TableSetupColumn("Switch", ImGuiTableColumnFlags.WidthFixed, 45);
                                        ImGui.TableSetupColumn("Part Texture");
                                        ImGui.TableHeadersRow();
                                        for (ushort i = 0; i < iNode->PartsList->PartCount; i++) {
                                            ImGui.TableNextColumn();

                                            if (i == iNode->PartId) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1));
                                            ImGui.Text($"#{i.ToString().PadLeft(iNode->PartsList->PartCount.ToString().Length, '0')}");
                                            if (i == iNode->PartId) ImGui.PopStyleColor(1);
                                            ImGui.TableNextColumn();

                                            if (ImGui.Button($"Switch##{(ulong)kernelTexture->D3D11ShaderResourceView:X}p{i}", new Vector2(-1, 23))) {
                                                iNode->PartId = i;
                                            }

                                            ImGui.TableNextColumn();
                                            var tPart = iNode->PartsList->Parts[i];
                                            var u = isHighResolution ? tPart.U * 2.0f : tPart.U;
                                            var v = isHighResolution ? tPart.V * 2.0f : tPart.V;
                                            var width = isHighResolution ? tPart.Width * 2.0f : tPart.Width;
                                            var height = isHighResolution ? tPart.Height * 2.0f : tPart.Height;

                                            if (ImGui.GetIO().KeyShift) {
                                                ImGui.Text($"[U1: {u / kernelTexture->Width }  V1: {v / kernelTexture->Height}  U2: {(u + width) / kernelTexture->Width}  V2: {(v + height) / kernelTexture->Height}]");
                                                if (ImGui.IsItemClicked()) {
                                                    ImGui.SetClipboardText($"new Vector2({u / kernelTexture->Width:F4}f, {v / kernelTexture->Height:F4}f), new Vector2({ (u + width) / kernelTexture->Width :F4}f, {(v + height) / kernelTexture->Height:F4}f),");
                                                }
                                            } else {
                                                ImGui.Text($"[U: {u}  V: {v}  W: {width}  H: {height}]");
                                            }
                                            
                                            ImGui.Image(new IntPtr(kernelTexture->D3D11ShaderResourceView), new Vector2(width, height), new Vector2(u , v) / textureSize, new Vector2(u + width, v + height) / textureSize);
                                        }
                                        ImGui.EndTable();
                                        ImGui.TreePop();
                                    }
                                    ImGui.TreePop();
                                }
                            } else if (texType == TextureType.KernelTexture) {
                                if (ImGui.TreeNode($"Texture##{(ulong) textureInfo->AtkTexture.KernelTexture->D3D11ShaderResourceView:X}")) {
                                    ImGui.Image(new IntPtr(textureInfo->AtkTexture.KernelTexture->D3D11ShaderResourceView), new Vector2(textureInfo->AtkTexture.KernelTexture->Width, textureInfo->AtkTexture.KernelTexture->Height));
                                    ImGui.TreePop();
                                }
                            }
                        }
                    } else {
                        ImGui.Text("no texture loaded");
                    }

                    if (node->Type != NodeType.Image) break;
                    ImGui.SetNextItemWidth(150);
                    ImGui.InputInt($"###inputIconId__{(ulong)node:X}", ref loadImageId);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(150);
                    ImGui.InputInt($"###inputIconVersion__{(ulong)node:X}", ref loadImageVersion);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Load Icon")) {
                        iNode->LoadIconTexture(loadImageId, loadImageVersion);
                    }
                        
                        
                        
                    break;
            }

            ImGui.TreePop();
        }
        else if(ImGui.IsItemHovered()) DrawOutline(node);

        if (isVisible && !popped && !textOnly)
            ImGui.PopStyleColor();
    }

    private static void PrintComponentNode(AtkResNode* node, string treePrefix, bool textOnly = false)
    {
        var compNode = (AtkComponentNode*)node;
        var componentInfo = compNode->Component->UldManager;
        var objectInfo = (AtkUldComponentInfo*)componentInfo.Objects;
        if (objectInfo == null) return;

        bool popped = false;
        bool isVisible = node->IsVisible;

        if (isVisible && !textOnly)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 255, 0, 255));

        var childCount = componentInfo.NodeListCount;

        if (elementSelectorFind.Length > 0) {
            ImGui.SetNextItemOpen(elementSelectorFind.Contains((ulong) node), ImGuiCond.Always);
        }
        if (textOnly) ImGui.SetNextItemOpen(false, ImGuiCond.Always);
        if (ImGui.TreeNode($"{treePrefix} [{NodeID(node->NodeID)}] {objectInfo->ComponentType} Component Node (ptr = {(long)node:X}, component ptr = {(long)compNode->Component:X}) child count = {childCount}  ###{(long)node}"))
        {
            if (ImGui.IsItemHovered()) DrawOutline(node);
            if (isVisible && !textOnly)
            {
                ImGui.PopStyleColor();
                popped = true;
            }

            ImGui.Text("Node: ");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)node:X}");
            ImGui.SameLine();
            DebugManager.PrintOutObject(*compNode, (ulong) compNode, new List<string>());
            ImGui.Text("Component: ");
            ImGui.SameLine();
            DebugManager.ClickToCopyText($"{(ulong)compNode->Component:X}");
            ImGui.SameLine();
                
            switch (objectInfo->ComponentType) {
                case ComponentType.Button: DebugManager.PrintOutObject(*(AtkComponentButton*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.Slider: DebugManager.PrintOutObject(*(AtkComponentSlider*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.Window: DebugManager.PrintOutObject(*(AtkComponentWindow*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.CheckBox: DebugManager.PrintOutObject(*(AtkComponentCheckBox*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.GaugeBar: DebugManager.PrintOutObject(*(AtkComponentGaugeBar*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.RadioButton: DebugManager.PrintOutObject(*(AtkComponentRadioButton*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.TextInput: DebugManager.PrintOutObject(*(AtkComponentTextInput*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.Icon: DebugManager.PrintOutObject(*(AtkComponentIcon*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.NumericInput: DebugManager.PrintOutObject(*(AtkComponentNumericInput*)compNode->Component, (ulong) compNode->Component, new List<string>()); break;
                case ComponentType.List: DebugManager.PrintOutObject(*(AtkComponentList*)compNode->Component, (ulong) compNode->Component); break;
                case ComponentType.TreeList: DebugManager.PrintOutObject(*(AtkComponentTreeList*)compNode->Component, (ulong) compNode->Component); break;
                case ComponentType.DropDownList: DebugManager.PrintOutObject((AtkComponentDropDownList*)compNode->Component); break;
                case ComponentType.ScrollBar:  DebugManager.PrintOutObject((AtkComponentScrollBar*)compNode->Component); break;
                case ComponentType.ListItemRenderer:  DebugManager.PrintOutObject((AtkComponentListItemRenderer*)compNode->Component); break;
                case ComponentType.IconText: DebugManager.PrintOutObject((AtkComponentIconText*)compNode->Component); break;
                case ComponentType.DragDrop: DebugManager.PrintOutObject((AtkComponentDragDrop*)compNode->Component); break;
                case ComponentType.GuildLeveCard: DebugManager.PrintOutObject((AtkComponentGuildLeveCard*)compNode->Component); break;
                case ComponentType.TextNineGrid: DebugManager.PrintOutObject((AtkComponentTextNineGrid*)compNode->Component); break;
                case ComponentType.JournalCanvas: DebugManager.PrintOutObject((AtkComponentJournalCanvas*)compNode->Component); break;
                case ComponentType.HoldButton: DebugManager.PrintOutObject((AtkComponentHoldButton*)compNode->Component); break;
                #if DEBUG
                case ComponentType.Portrait: DebugManager.PrintOutObject((AtkComponentPortrait*)compNode->Component); break;
                #endif
                default: DebugManager.PrintOutObject(*compNode->Component, (ulong) compNode->Component, new List<string>()); break;
            }
            
            ImGui.Text("Data: ");
            ImGui.SameLine();
            var componentData = componentInfo.ComponentData;
            DebugManager.ClickToCopy(componentData);
            if (componentData != null) {
                ImGui.SameLine();
                switch (objectInfo->ComponentType) {
                    case ComponentType.Base: DebugManager.PrintOutObject(componentData); break;
                    case ComponentType.Button: DebugManager.PrintOutObject((AtkUldComponentDataButton*)componentData); break;
                    case ComponentType.Window: DebugManager.PrintOutObject((AtkUldComponentDataWindow*)componentData); break;
                    case ComponentType.CheckBox: DebugManager.PrintOutObject((AtkUldComponentDataCheckBox*)componentData); break;
                    case ComponentType.RadioButton: DebugManager.PrintOutObject((AtkUldComponentDataRadioButton*)componentData); break;
                    case ComponentType.GaugeBar: DebugManager.PrintOutObject((AtkUldComponentDataGaugeBar*)componentData); break;
                    case ComponentType.Slider: DebugManager.PrintOutObject((AtkUldComponentDataSlider*)componentData); break;
                    case ComponentType.TextInput: DebugManager.PrintOutObject((AtkUldComponentDataTextInput*)componentData); break;
                    case ComponentType.NumericInput: DebugManager.PrintOutObject((AtkUldComponentDataNumericInput*)componentData); break;
                    case ComponentType.List: DebugManager.PrintOutObject((AtkUldComponentDataList*)componentData); break;
                    case ComponentType.DropDownList: DebugManager.PrintOutObject((AtkUldComponentDataDropDownList*)componentData); break;
                    case ComponentType.Tab: DebugManager.PrintOutObject((AtkUldComponentDataTab*)componentData); break;
                    case ComponentType.TreeList: DebugManager.PrintOutObject((AtkUldComponentDataTreeList*)componentData); break;
                    case ComponentType.ScrollBar: DebugManager.PrintOutObject((AtkUldComponentDataScrollBar*)componentData); break;
                    case ComponentType.ListItemRenderer: DebugManager.PrintOutObject((AtkUldComponentDataListItemRenderer*)componentData); break;
                    case ComponentType.Icon: DebugManager.PrintOutObject((AtkUldComponentDataIcon*)componentData); break;
                    case ComponentType.IconText: DebugManager.PrintOutObject((AtkUldComponentDataIconText*)componentData); break;
                    case ComponentType.DragDrop: DebugManager.PrintOutObject((AtkUldComponentDataDragDrop*)componentData); break;
                    case ComponentType.GuildLeveCard: DebugManager.PrintOutObject((AtkUldComponentDataGuildLeveCard*)componentData); break;
                    case ComponentType.TextNineGrid: DebugManager.PrintOutObject((AtkUldComponentDataTextNineGrid*)componentData); break;
                    case ComponentType.JournalCanvas: DebugManager.PrintOutObject((AtkUldComponentDataJournalCanvas*)componentData); break;
                    case ComponentType.Multipurpose: DebugManager.PrintOutObject((AtkUldComponentDataMultipurpose*)componentData); break;
                    case ComponentType.Map: DebugManager.PrintOutObject((AtkUldComponentDataMap*)componentData); break;
                    case ComponentType.Preview: DebugManager.PrintOutObject((AtkUldComponentDataPreview*)componentData); break;
                    case ComponentType.HoldButton: DebugManager.PrintOutObject((AtkUldComponentDataHoldButton*)componentData); break;
                    #if DEBUG
                    case ComponentType.Portrait: DebugManager.PrintOutObject((AtkUldComponentDataPortrait*)componentData); break;
                    #endif
                    default: DebugManager.PrintOutObject(componentData); break;
                }
            }
            
            

            PrintResNode(node);
            PrintNode(componentInfo.RootNode);

            switch (objectInfo->ComponentType)
            {
                case ComponentType.TextInput:
                    var textInputComponent = (AtkComponentTextInput*)compNode->Component;
                    ImGui.Text($"InputBase Text1: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->AtkComponentInputBase.UnkText1.StringPtr))}");
                    ImGui.Text($"InputBase Text2: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->AtkComponentInputBase.UnkText2.StringPtr))}");
                    ImGui.Text($"Text1: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->UnkText1.StringPtr))}");
                    ImGui.Text($"Text2: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->UnkText2.StringPtr))}");
                    ImGui.Text($"Text3: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->UnkText3.StringPtr))}");
                    ImGui.Text($"Text4: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->UnkText4.StringPtr))}");
                    ImGui.Text($"Text5: {Marshal.PtrToStringAnsi(new IntPtr(textInputComponent->UnkText5.StringPtr))}");
                    break;
                case ComponentType.List:
                case ComponentType.TreeList:
                    var l = (AtkComponentList*)compNode->Component;
                    if (ImGui.SmallButton("Inc.Selected")) {
                        l->SelectedItemIndex++;
                    }
                    break;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFAAAA);
            if (ImGui.TreeNode($"Node List##{(ulong) node:X}")) {
                ImGui.PopStyleColor();

                for (var i = 0; i < compNode->Component->UldManager.NodeListCount; i++) {
                    PrintNode(compNode->Component->UldManager.NodeList[i], false, $"[{i}] ");
                }

                ImGui.TreePop();
            } else {
                ImGui.PopStyleColor();
            }

            ImGui.TreePop();
        }
        else if (ImGui.IsItemHovered()) DrawOutline(node);


        if (isVisible && !popped && !textOnly)
            ImGui.PopStyleColor();
    }
        
    private static void PrintResNode(AtkResNode* node)
    {
        ImGui.Text($"NodeID: {NodeID(node->NodeID, false)}   Type: {node->Type}");
        ImGui.SameLine();
        if (ImGui.SmallButton($"T:Visible##{(ulong)node:X}")) {
            node->NodeFlags ^= NodeFlags.Visible;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"C:Ptr##{(ulong)node:X}")) {
            ImGui.SetClipboardText($"{(ulong)node:X}");
        }


        ImGui.Text(
            $"X: {node->X} Y: {node->Y} " +
            $"ScaleX: {node->ScaleX} ScaleY: {node->ScaleY} " +
            $"Rotation: {node->Rotation} " +
            $"Width: {node->Width} Height: {node->Height} " +
            $"OriginX: {node->OriginX} OriginY: {node->OriginY} " +
            $"Priority: {node->Priority}, Depth: {node->Depth}/{node->Depth_2} " +
            $"Flags: {node->NodeFlags} / {node->DrawFlags:X} " +
            $"DrawFlags: {node->DrawFlags:X}");
        ImGui.Text(
            $"RGBA: 0x{node->Color.R:X2}{node->Color.G:X2}{node->Color.B:X2}{node->Color.A:X2} " +
            $"AddRGB: {node->AddRed} {node->AddGreen} {node->AddBlue} " +
            $"MultiplyRGB: {node->MultiplyRed} {node->MultiplyGreen} {node->MultiplyBlue}");

        var v2 = new Vector2(node->X, node->Y);
        if (ImGui.InputFloat2("Position", ref v2)) {
            node->SetPositionFloat(v2.X, v2.Y);
        }

        var timeline = node->Timeline;
        if (timeline != null)
        {
            if (ImGui.TreeNode($"Timeline##{(ulong)node:X}"))
            {
                DrawAtkTimeline(node, timeline);

                ImGui.TreePop();
            }
        }

        var evt = node->AtkEventManager.Event;
        if (evt != null) {
            if (ImGui.TreeNode($"Events##{(ulong)node:X}")) {
                if (ImGui.BeginTable($"eventTable##{(ulong)node:X}", 7, ImGuiTableFlags.Resizable)) {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 30);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Param", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Flags", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Unk29", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("Listener", ImGuiTableColumnFlags.WidthFixed, 100);
                    
                    ImGui.TableHeadersRow();

                    var i = 0;
                    while (evt != null) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{i++}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{evt->Type}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{evt->Param}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{evt->Flags}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{evt->Unk29}");
                        ImGui.TableNextColumn();
                        DebugManager.PrintAddress(evt->Target);
                        ImGui.TableNextColumn();
                        DebugManager.PrintAddress(evt->Listener);
                        
                        evt = evt->NextEvent;
                    }
                    
                    ImGui.EndTable();
                }

                ImGui.TreePop();
            }
        }

    }

    private static void DrawAtkTimeline(AtkResNode* node, AtkTimeline* timeline)
    {
        ImGui.Text(
                    $"Parent Time: {timeline->ParentFrameTime:0.00} ({(ushort)(timeline->ParentFrameTime * 30)}) " +
                    $"Frame Time: {timeline->FrameTime:0.00} ({(ushort)(timeline->FrameTime * 30)}) " +
                    $"Animated Properties: {~timeline->Mask} Mask2: {timeline->Flags}");

        if (timeline->ActiveAnimation != null)
            DrawAtkAnimation("Active Animation", node, timeline->ActiveAnimation);

        ImGui.Text(
            $"Active Label Id: {timeline->ActiveLabelId} " +
            $"Duration: {timeline->LabelFrameIdxDuration} " +
            $"End Idx: {timeline->LabelEndFrameIdx}");

        if (timeline->Resource != null)
            DrawAtkTimelineResource("Resource:", node, timeline->Resource);

        if (timeline->LabelResource != null)
            DrawAtkTimelineResource("Label Resource:", node, timeline->LabelResource);
    }

    private static void DrawAtkTimelineResource(string name, AtkResNode* node, AtkTimelineResource* data)
    {
        ImGui.Text(
                $"Id: {data->Id}");

        if (ImGui.TreeNode($"{name} {data->AnimationCount} Animations##{(ulong)node:X}"))
        {
            if (data->Animations != null)
            {
                for (var i = 0; i < data->AnimationCount; ++i)
                    DrawAtkAnimation($"Anim {i}", node, &data->Animations[i]);
            }
            else
                ImGui.Text("Entries are null");
            ImGui.TreePop();
        }

        if (ImGui.TreeNode($"{name} {data->LabelSetCount} Label Datas##{(ulong)node:X}"))
        {
            if (data->LabelSets != null)
            {
                for (var i = 0; i < data->LabelSetCount; ++i)
                    DrawAtkLabelAnimation($"Label {i}", node, &data->LabelSets[i]);
            }
            else
                ImGui.Text("Entries are null");
            ImGui.TreePop();
        }
    }

    private static void DrawAtkAnimation(string name, AtkResNode* node, AtkTimelineAnimation* anim)
    {
        if (ImGui.TreeNode($"{name}({anim->StartFrameIdx} -> {anim->EndFrameIdx})##{(ulong)node:X}_{(ulong)anim:X}"))
        {
            ImGui.Text(
                $"Start: {anim->StartFrameIdx} " +
                $"End: {anim->EndFrameIdx}");
            var props = new Span<AtkTimelineKeyGroup>(Unsafe.AsPointer(ref anim->KeyGroups[0]), 8);
            var k = 0;
            foreach (var prop in props)
            {
                DrawAtkTimelineKeyGroup(FormatTimelineKeyGroupId(node->Type, k), node, &prop);
                k++;
            }
            ImGui.TreePop();
        }
    }

    private static void DrawAtkLabelAnimation(string name, AtkResNode* node, AtkTimelineLabelSet* anim)
    {
        if (ImGui.TreeNode($"{name}({anim->StartFrameIdx} -> {anim->EndFrameIdx})##{(ulong)node:X}_{(ulong)anim:X}"))
        {
            ImGui.Text(
                $"Start: {anim->StartFrameIdx} " +
                $"End: {anim->EndFrameIdx}");
            DrawAtkTimelineKeyGroup(FormatTimelineKeyGroupId(node->Type, 7), node, &anim->LabelKeyGroup);
            ImGui.TreePop();
        }
    }

    private static void DrawAtkTimelineKeyGroup(string name, AtkResNode* node, AtkTimelineKeyGroup* prop)
    {
        if (prop->Type != AtkTimelineKeyGroupType.None)
        {
            if (ImGui.TreeNode($"{name} - {prop->KeyFrameCount} Keyframes of {prop->Type}##{(ulong)node:X}_{(ulong)prop:X}"))
            {
                for (var keyframeIdx = 0; keyframeIdx < prop->KeyFrameCount; ++keyframeIdx)
                {
                    ref var keyframe = ref prop->KeyFrames[keyframeIdx];
                    ImGui.Text(
                        $"Frame: {keyframe.FrameIdx} " +
                        $"Interpolation: {keyframe.Interpolation} " +
                        $"({keyframe.SpeedCoefficient1:0.##}, {keyframe.SpeedCoefficient2:0.##})");
                    ref var v = ref keyframe.Value;
                    ImGui.Text("Value: " + prop->Type switch
                    {
                        AtkTimelineKeyGroupType.Float2 => $"({v.Float2.Item1}, {v.Float2.Item2})",
                        AtkTimelineKeyGroupType.Float => $"{v.Float}",
                        AtkTimelineKeyGroupType.Byte => $"{v.Byte}",
                        AtkTimelineKeyGroupType.NodeTint => $"Multiply: ({v.NodeTint.MultiplyRGB.R}, {v.NodeTint.MultiplyRGB.G}, {v.NodeTint.MultiplyRGB.B}) " +
                                                      $"Add: ({v.NodeTint.AddR}, {v.NodeTint.AddG}, {v.NodeTint.AddB})",
                        AtkTimelineKeyGroupType.UShort => $"{v.UShort}",
                        AtkTimelineKeyGroupType.RGB => $"({v.RGB.R}, {v.RGB.G}, {v.RGB.B})",
                        AtkTimelineKeyGroupType.Label => $"{v.Label.JumpBehavior}: {v.Label.LabelId}, {v.Label.JumpLabelId}",
                        _ => $"Unknown (0x{*(ulong*)Unsafe.AsPointer(ref v):X8})",
                    });
                }
                ImGui.TreePop();
            }
        }
    }

    private static string FormatTimelineKeyGroupId(NodeType nodeType, int propIdx) =>
        propIdx switch
        {
            0 => "Positon",
            1 => "Rotation",
            2 => "Scale",
            3 => "Alpha",
            4 => "Add/Multiply RGB",
            5 => nodeType switch {
                    NodeType.Image or NodeType.NineGrid => "Part ID",
                    NodeType.Text => "Text Color",
                    _ => "Node Specific"
                },
            6 => nodeType switch
            {
                NodeType.Text => "Edge Color",
                _ => "Node Specific"
            },
            7 => "Active Label",
            _ => "Undefined"
        } + $" ({propIdx})";


    private bool doingSearch;

    private bool DrawUnitListHeader(int index, uint count, ulong ptr, bool highlight) {
        ImGui.PushStyleColor(ImGuiCol.Text, highlight ? 0xFFAAAA00 : 0xFFFFFFFF);
        if (!string.IsNullOrEmpty(Plugin.PluginConfig.Debugging.AtkUnitBaseSearch) && !doingSearch) {
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        } else if (doingSearch && string.IsNullOrEmpty(Plugin.PluginConfig.Debugging.AtkUnitBaseSearch)) {
            ImGui.SetNextItemOpen(false, ImGuiCond.Always);
        }
        var treeNode = ImGui.TreeNode($"{listNames[index]}##unitList_{index}");
        ImGui.PopStyleColor();
            
        ImGui.SameLine();
        ImGui.TextDisabled($"C:{count}  {ptr:X}");
        return treeNode;
    }

    private void DrawUnitBaseList() {

        bool foundSelected = false;
        bool noResults = true;
        var stage = AtkStage.GetSingleton();
                
        var unitManagers = &stage->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
            
        var searchStr = Plugin.PluginConfig.Debugging.AtkUnitBaseSearch;
        var searching = !string.IsNullOrEmpty(searchStr);
            
        for (var i = 0; i < UnitListCount; i++) {

            var headerDrawn = false;
                
            var highlight = selectedUnitBase != null && selectedInList[i];
            selectedInList[i] = false;
            var unitManager = &unitManagers[i];

            var headerOpen = true;
                
            if (!searching) {
                headerOpen = DrawUnitListHeader(i, unitManager->Count, (ulong) unitManager, highlight);
                headerDrawn = true;
                noResults = false;
            }
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->EntriesSpan.Length))) {
                var unitBase = unitManager->EntriesSpan[j].Value;
                if (unitBase == null) continue;
                if (selectedUnitBase != null && unitBase == selectedUnitBase) {
                    selectedInList[i] = true;
                    foundSelected = true;
                }
                var name = Marshal.PtrToStringAnsi(new IntPtr(unitBase->Name));
                if (searching) {
                    if (name == null || !name.ToLower().Contains(searchStr.ToLower())) continue;
                }
                noResults = false;
                if (!headerDrawn) {
                    headerOpen = DrawUnitListHeader(i, unitManager->Count, (ulong) unitManager, highlight);
                    headerDrawn = true;
                }
                    
                if (headerOpen) {
                    var visible = (unitBase->Flags & 0x20) == 0x20;
                    ImGui.PushStyleColor(ImGuiCol.Text, visible ? 0xFF00FF00 : 0xFF999999);
                        
                    if (ImGui.Selectable($"{name}##list{i}-{(ulong) unitBase:X}_{j}", selectedUnitBase == unitBase)) {
                        selectedUnitBase = unitBase;
                        foundSelected = true;
                        selectedInList[i] = true;
                        Plugin.PluginConfig.Debugging.SelectedAtkUnitBase = (ulong) selectedUnitBase;
                        Plugin.PluginConfig.Save();
                    }
                    ImGui.PopStyleColor();
                }

            }

            if (headerDrawn && headerOpen) {
                ImGui.TreePop();
            }
                
            if (selectedInList[i] == false && selectedUnitBase != null) {
                foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->EntriesSpan.Length))) {
                    var unitBase = unitManager->EntriesSpan[j].Value;
                    if (selectedUnitBase == null || unitBase != selectedUnitBase) continue;
                    selectedInList[i] = true;
                    foundSelected = true;
                }
            }

        }

        if (noResults) {
            ImGui.TextDisabled("No Results");
        }
            
        if (!foundSelected) {
            selectedUnitBase = null;
            Plugin.PluginConfig.Debugging.SelectedAtkUnitBase = 0;
        }

            
        if (doingSearch && string.IsNullOrEmpty(Plugin.PluginConfig.Debugging.AtkUnitBaseSearch)) {
            doingSearch = false;
        } else if (!doingSearch && !string.IsNullOrEmpty(Plugin.PluginConfig.Debugging.AtkUnitBaseSearch)) {
            doingSearch = true;
        }
    }
        
        
    private static Vector2 GetNodePosition(AtkResNode* node) {
        var pos = new Vector2(node->X, node->Y);
        pos -= new Vector2(node->OriginX * (node->ScaleX - 1), node->OriginY * (node->ScaleY - 1));
        var par = node->ParentNode;
        while (par != null) {
            pos *= new Vector2(par->ScaleX, par->ScaleY);
            pos += new Vector2(par->X, par->Y);
            pos -= new Vector2(par->OriginX * (par->ScaleX - 1), par->OriginY * (par->ScaleY - 1));
            par = par->ParentNode;
        }
        return pos;
    }

    private static Vector2 GetNodeScale(AtkResNode* node) {
        if (node == null) return new Vector2(1, 1);
        var scale = new Vector2(node->ScaleX, node->ScaleY);
        while (node->ParentNode != null) {
            node = node->ParentNode;
            scale *= new Vector2(node->ScaleX, node->ScaleY);
        }
        return scale;
    }

    private static bool GetNodeVisible(AtkResNode* node) {
        if (node == null) return false;
        while (node != null) {
            if (!node->IsVisible) return false;
            node = node->ParentNode;
        }
        return true;
    }

    private static void DrawOutline(AtkResNode* node) {
        var position = GetNodePosition(node);
        var scale = GetNodeScale(node);
        var size = new Vector2(node->Width, node->Height) * scale;
            
        var nodeVisible = GetNodeVisible(node);
        position += ImGui.GetMainViewport().Pos;
        ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(position, position + size, nodeVisible ? 0xFF00FF00 : 0xFF0000FF);
    }

    private class NodeState {
        public Vector2 Position;
        public Vector2 SecondPosition;
        public Vector2 Size;
        public bool Visible;
    }

    private NodeState GetNodeState(AtkResNode* node) {
        var position = GetNodePosition(node);
        var scale = GetNodeScale(node);
        var size = new Vector2(node->Width, node->Height) * scale;
        return new NodeState() {
            Position = position,
            SecondPosition = position + size,
            Visible = GetNodeVisible(node),
            Size = size,
        };
    }
        
    public override string Name => "UI Debugging";
}
