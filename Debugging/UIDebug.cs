using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin.GameStructs;
using SimpleTweaksPlugin.GameStructs.Client.UI;
using SimpleTweaksPlugin.Helper;

// Customised version of https://github.com/aers/FFXIVUIDebug


namespace SimpleTweaksPlugin.Debugging {

    public partial class DebugConfig {
        public ulong SelectedAtkUnitBase;
        public string AtkUnitBaseSearch = string.Empty;
    }
    
    
    public unsafe class UIDebug : DebugHelper {
        private bool firstDraw = true;
        private AtkUnitBase* selectedUnitBase = null;
        
        private delegate AtkStage* GetAtkStageSingleton();
        private GetAtkStageSingleton getAtkStageSingleton;
        
        private const int UnitListCount = 18;
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

        public override void Draw() {
            if (firstDraw) {
                firstDraw = false;
                selectedUnitBase = (AtkUnitBase*) (Plugin.PluginConfig.Debugging.SelectedAtkUnitBase);
            }
            if (getAtkStageSingleton == null) {
                var getSingletonAddr = Plugin.PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 B8 01 00 00 00 48 8D 15 ?? ?? ?? ?? 48 8B 48 20 E8 ?? ?? ?? ?? 48 8B CF");
                this.getAtkStageSingleton = Marshal.GetDelegateForFunctionPointer<GetAtkStageSingleton>(getSingletonAddr);
            }

            ImGui.BeginChild("st_uiDebug_unitBaseSelect", new Vector2(250, -1), true);
            
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("###atkUnitBaseSearch", "Search", ref Plugin.PluginConfig.Debugging.AtkUnitBaseSearch, 0x20);
            
            DrawUnitBaseList();
            ImGui.EndChild();
            if (selectedUnitBase != null) {
                ImGui.SameLine();
                ImGui.BeginChild("st_uiDebug_selectedUnitBase", new Vector2(-1, -1), true);
                DrawUnitBase(selectedUnitBase);
                ImGui.EndChild();
            }
        }
        
        private void DrawUnitBase(AtkUnitBase* atkUnitBase) {

            var isVisible = (atkUnitBase->Flags & 0x20) == 0x20;
            string addonName = Marshal.PtrToStringAnsi(new IntPtr(atkUnitBase->Name));
            
            ImGui.Text($"{addonName}");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, isVisible ? 0xFF00FF00 : 0xFF0000FF);
            ImGui.Text(isVisible ? "Visible" : "Not Visible");
            ImGui.PopStyleColor();
            
            ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - 25);
            if (ImGui.SmallButton("V")) {
                atkUnitBase->Flags ^= 0x20;
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
            ImGui.Text($"Widget Count {atkUnitBase->ULDData.ObjectCount}");
            
            ImGui.Separator();

            object addonObj = addonName switch {
                "ActionDetail" => *(AddonActionDetail*) atkUnitBase,
                "_ActionBar" => *(AddonActionBarBase*) atkUnitBase,
                _ => *atkUnitBase
            };


            DebugManager.PrintOutObject(addonObj, (ulong) atkUnitBase, new List<string>());

            ImGui.Dummy(new Vector2(25 * ImGui.GetIO().FontGlobalScale));
            ImGui.Separator();
            if (atkUnitBase->RootNode != null)
                PrintNode(atkUnitBase->RootNode);

            
            if (atkUnitBase->ULDData.NodeListCount > 0) {
                ImGui.Dummy(new Vector2(25 * ImGui.GetIO().FontGlobalScale));
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFAAAA);
                if (ImGui.TreeNode($"Node List##{(ulong)atkUnitBase:X}")) {
                    ImGui.PopStyleColor();

                    for (var j = 0; j < atkUnitBase->ULDData.NodeListCount; j++) {
                        PrintNode(atkUnitBase->ULDData.NodeList[j], false, $"[{j}] ");
                    }

                    ImGui.TreePop();
                } else {
                    ImGui.PopStyleColor();
                }
            }
        }

        
        private void PrintNode(AtkResNode* node, bool printSiblings = true, string treePrefix = "")
        {
            if (node == null)
                return;

            if ((int)node->Type < 1000)
                PrintSimpleNode(node, treePrefix);
            else
                PrintComponentNode(node, treePrefix);

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
        
        private void PrintSimpleNode(AtkResNode* node, string treePrefix)
        {
            bool popped = false;
            bool isVisible = (node->Flags & 0x10) == 0x10;

            if (isVisible)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 255, 0, 255));

            if (ImGui.TreeNode($"{treePrefix}{node->Type} Node (ptr = {(long)node:X})###{(long)node}"))
            {
                if (ImGui.IsItemHovered()) DrawOutline(node);
                if (isVisible)
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

                        ImGui.InputText($"Replace Text##{(ulong) textNode:X}", new IntPtr(textNode->NodeText.StringPtr), (uint) textNode->NodeText.BufSize);


                        ImGui.Text($"AlignmentType: {(AlignmentType)textNode->AlignmentFontType}  FontSize: {textNode->FontSize}");
                        int b = textNode->AlignmentFontType;
                        if (ImGui.InputInt($"###setAlignment{(ulong) textNode:X}", ref b, 1)) {
                            while (b > byte.MaxValue) b -= byte.MaxValue;
                            while (b < byte.MinValue) b += byte.MaxValue;
                            textNode->AlignmentFontType = (byte) b;
                            textNode->AtkResNode.Flags_2 |= 0x1;
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
                        ImGui.Text($"text: {Marshal.PtrToStringAnsi(new IntPtr(counterNode->NodeText.StringPtr))}");
                        break;
                    case NodeType.Image:
                        var imageNode = (AtkImageNode*)node;
                        if (imageNode->PartsList != null) {
                            if (imageNode->PartId > imageNode->PartsList->PartCount) {
                                ImGui.Text("part id > part count?");
                            } else {
                                var textureInfo = imageNode->PartsList->Parts[imageNode->PartId].ULDTexture;
                                var texType = textureInfo->AtkTexture.TextureType;
                                ImGui.Text($"texture type: {texType} part_id={imageNode->PartId} part_id_count={imageNode->PartsList->PartCount}");
                                if (texType == TextureType.Resource) {
                                    var texFileNamePtr = textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                                    var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                                    ImGui.Text($"texture path: {texString}");
                                    var kernelTexture = textureInfo->AtkTexture.Resource->KernelTextureObject;
                                    
                                    if (ImGui.TreeNode($"Texture##{(ulong) kernelTexture->D3D11ShaderResourceView:X}")) {
                                        ImGui.Image(new IntPtr(kernelTexture->D3D11ShaderResourceView), new Vector2(kernelTexture->Width, kernelTexture->Height));
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
                        break;
                }

                ImGui.TreePop();
            }
            else if(ImGui.IsItemHovered()) DrawOutline(node);

            if (isVisible && !popped)
                ImGui.PopStyleColor();
        }

        private void PrintComponentNode(AtkResNode* node, string treePrefix)
        {
            var compNode = (AtkComponentNode*)node;

            bool popped = false;
            bool isVisible = (node->Flags & 0x10) == 0x10;

            if (isVisible)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 255, 0, 255));

            var componentInfo = compNode->Component->ULDData;

            var childCount = componentInfo.NodeListCount;

            var objectInfo = (ULDComponentInfo*)componentInfo.Objects;
            if (ImGui.TreeNode($"{treePrefix}{objectInfo->ComponentType} Component Node (ptr = {(long)node:X}, component ptr = {(long)compNode->Component:X}) child count = {childCount}  ###{(long)node}"))
            {
                if (ImGui.IsItemHovered()) DrawOutline(node);
                if (isVisible)
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
                    default: DebugManager.PrintOutObject(*compNode->Component, (ulong) compNode->Component, new List<string>()); break;
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
                }

                ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFAAAA);
                if (ImGui.TreeNode($"Node List##{(ulong) node:X}")) {
                    ImGui.PopStyleColor();

                    for (var i = 0; i < compNode->Component->ULDData.NodeListCount; i++) {
                        PrintNode(compNode->Component->ULDData.NodeList[i], false, $"[{i}] ");
                    }

                    ImGui.TreePop();
                } else {
                    ImGui.PopStyleColor();
                }

                ImGui.TreePop();
            }
            else if (ImGui.IsItemHovered()) DrawOutline(node);


            if (isVisible && !popped)
                ImGui.PopStyleColor();
        }
        
        private void PrintResNode(AtkResNode* node)
        {
            ImGui.Text($"NodeID: {node->NodeID}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"T:Visible##{(ulong)node:X}")) {
                node->Flags ^= 0x10;
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
                $"OriginX: {node->OriginX} OriginY: {node->OriginY}");
            ImGui.Text(
                $"RGBA: 0x{node->Color.R:X2}{node->Color.G:X2}{node->Color.B:X2}{node->Color.A:X2} " +
                $"AddRGB: {node->AddRed} {node->AddGreen} {node->AddBlue} " +
                $"MultiplyRGB: {node->MultiplyRed} {node->MultiplyGreen} {node->MultiplyBlue}");
        }


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
            var stage = getAtkStageSingleton();
                
            var unitManagers = &stage->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
            
            var searchStr = Plugin.PluginConfig.Debugging.AtkUnitBaseSearch;
            var searching = !string.IsNullOrEmpty(searchStr);
            
            for (var i = 0; i < UnitListCount; i++) {

                var headerDrawn = false;
                
                var highlight = selectedUnitBase != null && selectedInList[i];
                selectedInList[i] = false;
                var unitManager = &unitManagers[i];

                var unitBaseArray = &(unitManager->AtkUnitEntries);

                var headerOpen = true;
                
                if (!searching) {
                    headerOpen = DrawUnitListHeader(i, unitManager->Count, (ulong) unitManager, highlight);
                    headerDrawn = true;
                    noResults = false;
                }
                
                for (var j = 0; j < unitManager->Count && headerOpen; j++) {
                    var unitBase = unitBaseArray[j];
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
                    for (var j = 0; j < unitManager->Count; j++) {
                        if (selectedUnitBase == null || unitBaseArray[j] != selectedUnitBase) continue;
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
        
        
        private Vector2 GetNodePosition(AtkResNode* node) {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null) {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }
            return pos;
        }

        private Vector2 GetNodeScale(AtkResNode* node) {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null) {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }
            return scale;
        }

        private bool GetNodeVisible(AtkResNode* node) {
            if (node == null) return false;
            while (node != null) {
                if ((node->Flags & (short)NodeFlags.Visible) != (short)NodeFlags.Visible) return false;
                node = node->ParentNode;
            }
            return true;
        }

        private void DrawOutline(AtkResNode* node) {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            
            var nodeVisible = GetNodeVisible(node);
            ImGui.GetForegroundDrawList().AddRect(position, position + size, nodeVisible ? 0xFF00FF00 : 0xFF0000FF);
        }
        
        public override string Name => "UI Debugging";
    }
}
