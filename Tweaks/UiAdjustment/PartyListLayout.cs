using System;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class PartyListLayout : UiAdjustments.SubTweak {
        public override string Name => "Party List Layout";
        public override string Description => "Change the number of columns used in the party list.";

        public override bool Experimental => true;

        public class Configs : TweakConfig {
            [TweakConfigOption("Column Count", EditorSize = 150, IntMin = 1, IntMax = 8, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
            public int Columns = 1;

            [TweakConfigOption("Extra X Separation", EditorSize = 350, IntMin = -50, IntMax = 200, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
            public int XOffset = 0;
            
            [TweakConfigOption("Extra Y Separation", EditorSize = 350, IntMin = -50, IntMax = 200, IntType = TweakConfigOptionAttribute.IntEditType.Slider)]
            public int YOffset = 0;
        }
        
        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        private delegate void* PartyListOnUpdate(AddonPartyList* @this, void* a2, void* a3);
        private HookWrapper<PartyListOnUpdate> partyListOnUpdateHook;
        
        
        public override void Enable() {
            Config = LoadConfig<Configs>() ?? new Configs();
            partyListOnUpdateHook ??= Common.Hook<PartyListOnUpdate>("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B 7A 20", PartyListUpdateDetour, false);
            partyListOnUpdateHook?.Enable();
            base.Enable();
        }

        private void* PartyListUpdateDetour(AddonPartyList* @this, void* a2, void* a3) {
            var ret = partyListOnUpdateHook.Original(@this, a2, a3);
            try {
                Update(@this);
            } catch (Exception ex) {
                SimpleLog.Error(ex);
            }
            return ret;
        }

        public override void Disable() {
            partyListOnUpdateHook?.Disable();
            SaveConfig(Config);
            Update(Common.GetUnitBase<AddonPartyList>(), true);
            base.Disable();
        }

        private const int XSeparation = 260;
        private const int YSeparation = 80;
        
        private void Update(AddonPartyList* partyList, bool reset = false) {
            if (partyList == null) return;
            
            if (partyList->AtkUnitBase.UldManager.NodeListSize < 17) return;
            var visibleIndex = 0;

            var maxX = 0;
            var maxY = 0;
            
            for (var i = 17; i >= 5; i--) {
                var cNode = (AtkComponentNode*) partyList->AtkUnitBase.UldManager.NodeList[i];
                
                if (cNode->AtkResNode.IsVisible || reset) {
                    UpdateSlot(cNode, visibleIndex, ref maxX, ref maxY, reset);
                }
                if (cNode->AtkResNode.IsVisible) visibleIndex++;

                if (i == 6 && cNode->AtkResNode.IsVisible) {
                    // Chocobo Timer
                    partyList->MpBarSpecialResNode->SetPositionFloat(153 + cNode->AtkResNode.X, 44 + cNode->AtkResNode.Y);
                }
            }
            
            // Collision Node Update
            partyList->AtkUnitBase.UldManager.NodeList[1]->SetWidth(reset ? (ushort)500 : (ushort) maxX);
            partyList->AtkUnitBase.UldManager.NodeList[1]->SetHeight(reset ? (ushort)480 : (ushort) maxY);
            
            // Background Update
            partyList->AtkUnitBase.UldManager.NodeList[3]->ToggleVisibility(reset);
            
        }

        private void UpdateSlot(AtkComponentNode* cNode, int visibleIndex, ref int maxX, ref int maxY, bool reset, int? forceColumnCount = null) {
            var c = cNode->Component;
            
            c->UldManager.NodeList[0]->SetWidth(reset ? (ushort)366 : (ushort)(260 + Config.XOffset)); // Collision Node
            c->UldManager.NodeList[1]->SetWidth(reset ? (ushort)367 : (ushort)(260 + Config.XOffset));
            c->UldManager.NodeList[2]->SetWidth(reset ? (ushort)320 : (ushort)(260 + Config.XOffset)); 
            c->UldManager.NodeList[3]->SetWidth(reset ? (ushort)320 : (ushort)(260 + Config.XOffset));

            if (reset) {
                cNode->AtkResNode.SetPositionFloat(0, visibleIndex * 40);
            } else {
                var columnCount = forceColumnCount ?? Config.Columns;
                var columnIndex = visibleIndex % columnCount;
                var rowIndex = visibleIndex / columnCount;

                cNode->AtkResNode.SetPositionFloat(columnIndex * (XSeparation + Config.XOffset), rowIndex * (YSeparation + Config.YOffset));
                
                var xM = (columnIndex + 1) * (XSeparation + Config.XOffset);
                var yM = (rowIndex + 1) * (Config.YOffset + YSeparation) + 16;
                
                if (xM > maxX) maxX = xM;
                if (yM > maxY) maxY = yM;
            }
            
            var iconX = reset ? (short)263 : (short)59;
            for (var i = 14; i >= 5; i--) {
                var itcNode = c->UldManager.NodeList[i];

                itcNode->SetPositionShort(iconX, reset ? (short)12 : (short)65);
                iconX += 25;
            }

        }
    }
}
