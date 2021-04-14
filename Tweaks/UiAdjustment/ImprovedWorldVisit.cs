using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Internal;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using SimpleTweaksPlugin.Helper;
using AlignmentType = FFXIVClientStructs.FFXIV.Component.GUI.AlignmentType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ImprovedWorldVisit : UiAdjustments.SubTweak {

        public override string Name => "Cleaner World Visit Menu";
        public override string Description => "Cleans up the world visit menu and shows your current location in order on the list.";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += FrameworkOnOnUpdateEvent;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= FrameworkOnOnUpdateEvent;
            base.Disable();
        }
        
        public class WorldTravelSelect {
            public bool IsValid { get; }
            public AtkUnitBase* UnitBase;

            public AtkResNode* RootNode;

            public AtkComponentNode* WindowComponent;
            public AtkComponentNode* InformationBox;
            public AtkNineGridNode* InformationBoxBorder;
            public AtkComponentNode* WorldListComponent;
            public AtkTextNode* WorldListHeader;
            public AtkResNode* CurrentWorldContainer;
            public AtkResNode* HomeWorldContainer;
            public AtkTextNode* WorldInfoHeader;

            public AtkTextNode* CurrentWorldName;
            public AtkNineGridNode* CurrentWorldHeaderUnderline;
            public AtkTextNode* CurrentWorldHeader;
            public AtkImageNode* CurrentWorldIcon;

            public WorldTravelSelect(AtkUnitBase* address) {
                UnitBase = address;
                try {
                    RootNode = UnitBase->RootNode;
                    if (RootNode == null) return;
                    WindowComponent = (AtkComponentNode*) RootNode->ChildNode;
                    InformationBox = (AtkComponentNode*) WindowComponent->AtkResNode.PrevSiblingNode;
                    InformationBoxBorder = (AtkNineGridNode*) InformationBox->AtkResNode.PrevSiblingNode;
                    WorldListComponent = (AtkComponentNode*) InformationBoxBorder->AtkResNode.PrevSiblingNode;
                    WorldListHeader = (AtkTextNode*) WorldListComponent->AtkResNode.PrevSiblingNode;
                    CurrentWorldContainer = WorldListHeader->AtkResNode.PrevSiblingNode;
                    HomeWorldContainer = CurrentWorldContainer->PrevSiblingNode;
                    WorldInfoHeader = (AtkTextNode*) HomeWorldContainer->PrevSiblingNode;

                    CurrentWorldName = (AtkTextNode*) CurrentWorldContainer->ChildNode;
                    CurrentWorldHeaderUnderline = (AtkNineGridNode*)CurrentWorldName->AtkResNode.PrevSiblingNode;
                    CurrentWorldHeader = (AtkTextNode*) CurrentWorldHeaderUnderline->AtkResNode.PrevSiblingNode;
                    CurrentWorldIcon = (AtkImageNode*) CurrentWorldHeader->AtkResNode.PrevSiblingNode;

                    IsValid = true;
                } catch(Exception ex){
                    SimpleLog.Error(ex);
                    IsValid = false;
                }
            }
            
            public void DoCleanup(short? x, short? y) {
                UiHelper.Hide(InformationBox);
                UiHelper.Hide(InformationBoxBorder);
                UiHelper.Hide(WorldInfoHeader);
                UiHelper.Hide(HomeWorldContainer);
                UiHelper.Hide(CurrentWorldHeader);
                UiHelper.Hide(CurrentWorldHeaderUnderline);
                UiHelper.Hide(WorldListHeader);
                UiHelper.Hide(CurrentWorldContainer);
                UiHelper.SetSize(CurrentWorldContainer, null, 24);
                UiHelper.SetSize(RootNode, 250, null);
                UiHelper.SetWindowSize(WindowComponent, 250, 300);
                UiHelper.SetPosition(WorldListComponent, 20, 44);
                UiHelper.SetPosition(CurrentWorldContainer, 20, 0);
                UiHelper.SetPosition(CurrentWorldName, 28, 0);
                UiHelper.SetSize(CurrentWorldName, null, 24);
                UiHelper.Hide(CurrentWorldHeader);
                UiHelper.Hide(CurrentWorldHeaderUnderline);

                CurrentWorldName->AlignmentFontType = (byte)AlignmentType.Left;
                CurrentWorldName->TextColor = new ByteColor() {A = 255, R = 255, G = 255, B = 255 };
                CurrentWorldName->EdgeColor = new ByteColor() { A = 255, R = 156, G = 129, B = 56 };
                CurrentWorldName->TextFlags |= (byte) TextFlags.Edge;

                if (x != null && y != null) {
                    // Keep track of position to avoid window being bumped to the side when rebuilding 
                    UnitBase->X = x.Value;
                    UnitBase->Y = y.Value;
                    UiHelper.SetPosition(RootNode, x, y);
                }
            }

            public void TrySort() {
                var nodeList = (AtkComponentNode**) WorldListComponent->Component->UldManager.NodeList;

                var c = 0;
                var nodes = new AtkComponentNode*[18];
                var names = new string[nodes.Length];

                for (var i = 0; i < nodes.Length; i++) {
                    var n = nodeList[i + 3];
                    if (n->AtkResNode.Y == 0) continue;
                    var nameNode = (AtkTextNode*) n->Component->UldManager.NodeList[4];
                    var name = Marshal.PtrToStringAnsi(new IntPtr(nameNode->NodeText.StringPtr));
                    names[c] = name;
                    nodes[c++] = n;
                    
                }

                if (c == 0) return;


                var inserted = false;
                var currentServerName = Marshal.PtrToStringAnsi(new IntPtr(CurrentWorldName->NodeText.StringPtr));
                for (var i = 0; i < c; i++) {
                    if (!inserted) {
                        var s = string.Compare(names[i], currentServerName, StringComparison.InvariantCultureIgnoreCase);
                        if (s > 0) {
                            UiHelper.SetPosition(CurrentWorldContainer, 20,  44 + (i + 1) * 24);
                            inserted = true;
                        }
                    }

                    if (!inserted) continue;
                    nodes[i]->AtkResNode.Y += 24;
                    nodes[i]->AtkResNode.Flags_2 |= 0x1;
                }

                if (!inserted) {
                    UiHelper.SetPosition(CurrentWorldContainer, 20, 44 + (c + 1) * 24);
                }
                
                UiHelper.SetWindowSize(WindowComponent, null, (ushort) (44 + (c + 3) * 24));
                UiHelper.SetSize(RootNode, null,  (ushort)(44 + (c + 3) * 24));
                UiHelper.Show(CurrentWorldContainer);
            }
        }

        private short? windowX;
        private short? windowY;


        public void CheckWindow() {
            var ui = (AtkUnitBase*)PluginInterface.Framework.Gui.GetUiObjectByName("WorldTravelSelect", 1);
            if (ui == null) return;
            var window = new WorldTravelSelect(ui);
            if (!window.IsValid) return;
            if (window.RootNode->Width == 780) {
                window.DoCleanup(windowX, windowY);
            } else if (window.CurrentWorldContainer->Y == 0) {
                window.TrySort();
            } else {
                windowX = window.UnitBase->X;
                windowY = window.UnitBase->Y;
            }
        }

        private void FrameworkOnOnUpdateEvent(Framework framework) {
            try {
                CheckWindow();
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }
    }
}
