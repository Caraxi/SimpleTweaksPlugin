using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Helper;
using Lumina.Excel.GeneratedSheets;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class ReducedDeepDungeonInfo : UiAdjustments.SubTweak {
        public override string Name => "Reduced Deep Dungeon Info";
        public override string Description => "Removes the redundant infos from the deep dungeon character info.";
        protected override string Author => "Aireil";

        public override void Enable() {
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            base.Enable();
        }

        public override void Disable() {
            PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            base.Disable();
            UpdateDeepDungeonStatus(true);
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
                UpdateDeepDungeonStatus();
            }
            catch (Exception ex) {
                SimpleLog.Error(ex);
            }
        }

        private int limiter;

        private void UpdateDeepDungeonStatus(bool reset = false) {
            var deepDungeonUnitBase = Common.GetUnitBase("DeepDungeonStatus");
            if (deepDungeonUnitBase == null)
            {
                limiter = -10;
                return;
            }
            if (deepDungeonUnitBase->UldManager.NodeList == null ||
                deepDungeonUnitBase->UldManager.NodeListCount < 84) return;

            var resNode = deepDungeonUnitBase->UldManager.NodeList[0];
            var windowCollisionNode = (AtkCollisionNode*) deepDungeonUnitBase->UldManager.NodeList[1];
            var windowNode = (AtkComponentNode*) deepDungeonUnitBase->UldManager.NodeList[2];
            var itemsEffectsInfoNode = deepDungeonUnitBase->UldManager.NodeList[3];
            var magiciteInfoNode = deepDungeonUnitBase->UldManager.NodeList[23];
            var itemsInfoNode = deepDungeonUnitBase->UldManager.NodeList[33];
            var gearInfoNode = deepDungeonUnitBase->UldManager.NodeList[71];

            var armAetherpoolNode = (AtkComponentNode*) deepDungeonUnitBase->UldManager.NodeList[73];
            var armAetherpoolTextNode = (AtkTextNode*) armAetherpoolNode->Component->UldManager.NodeList[1];
            var armorAetherpoolNode = (AtkComponentNode*) deepDungeonUnitBase->UldManager.NodeList[72];
            var armorAetherpoolTextNode = (AtkTextNode*) armorAetherpoolNode->Component->UldManager.NodeList[1];
            var textNode = (AtkTextNode*) deepDungeonUnitBase->UldManager.NodeList[77]; // "To next level:" node

            var isHoh = magiciteInfoNode->IsVisible;

            if (reset) {
                UiHelper.Show(gearInfoNode);
                deepDungeonUnitBase->UldManager.NodeList[76]->Color.A = 255;
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[76]); // Job infos
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[78]);
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[79]);
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[80]);
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[81]);
                UiHelper.Show(deepDungeonUnitBase->UldManager.NodeList[82]);

                UiHelper.SetPosition(itemsEffectsInfoNode, null, isHoh ? 486 : 410);
                if (isHoh)
                    UiHelper.SetPosition(magiciteInfoNode, null, 412);
                UiHelper.SetPosition(itemsInfoNode, null, 246);
                UiHelper.SetSize(resNode, null, isHoh ? 616 : 540);
                UiHelper.SetSize(windowCollisionNode, null, isHoh ? 616 : 540);
                SetDeepdungeonWindow(windowNode, null, (ushort) (isHoh ? 616 : 540));

                UiHelper.SetPosition(textNode, 78, 65);
                textNode->FontSize = 12;
                textNode->AlignmentFontType = 3;
                UiHelper.SetText(textNode, PluginInterface.Data.Excel.GetSheet<Addon>().GetRow(10430).Text);

                return;
            }

            UiHelper.Hide(gearInfoNode);
            deepDungeonUnitBase->UldManager.NodeList[76]->Color.A = 0;
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[76]); // Job infos
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[78]);
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[79]);
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[80]);
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[81]);
            UiHelper.Hide(deepDungeonUnitBase->UldManager.NodeList[82]);

            UiHelper.SetPosition(itemsEffectsInfoNode, null, isHoh ? 270 : 194);
            if (isHoh)
                UiHelper.SetPosition(magiciteInfoNode, null, 196);
            UiHelper.SetPosition(itemsInfoNode, null, 30);
            UiHelper.SetSize(resNode, null, isHoh ? 398 : 322);
            UiHelper.SetSize(windowCollisionNode, null, isHoh ? 398 : 322);
            SetDeepdungeonWindow(windowNode, null, (ushort) (isHoh ? 398 : 322));

            // Limit expensive SeString manipulations
            switch (limiter)
            {
                case > 0:
                    limiter--;
                    return;
                // Burst when the window is created
                case < 0:
                    limiter++;
                    break;
                case 0:
                    limiter = 50;
                    break;
            }

            UiHelper.SetPosition(textNode, 148, 0);
            textNode->FontSize = 14;
            textNode->AlignmentFontType = 5;
            var armAetherpoolSeStr = Plugin.Common.ReadSeString(armAetherpoolTextNode->NodeText.StringPtr);
            var armorAetherpoolSeStr = Plugin.Common.ReadSeString(armorAetherpoolTextNode->NodeText.StringPtr);

            var payloads = new List<Payload>();
            payloads.AddRange(GetAetherpoolPayloads(armAetherpoolSeStr));
            payloads.Add(new TextPayload("/"));
            payloads.AddRange(GetAetherpoolPayloads(armorAetherpoolSeStr));

            UiHelper.SetText(textNode, new SeString(payloads));
        }

        private static void SetDeepdungeonWindow(AtkComponentNode* windowNode, ushort? width, ushort? height)
        {
            width ??= windowNode->AtkResNode.Width;
            height ??= windowNode->AtkResNode.Height;

            var n = windowNode->Component->UldManager.RootNode;
            UiHelper.SetSize(windowNode, width, height);  // Window
            UiHelper.SetSize(n, width, height);  // Collision
            n = n->PrevSiblingNode->PrevSiblingNode;
            UiHelper.SetSize(n, width - 2, height - 2); // Background
            n = n->PrevSiblingNode;
            UiHelper.SetSize(n, width - 2, height - 2); // Focused Border

            windowNode->AtkResNode.Flags_2 |= 0x1;
        }

        private IEnumerable<Payload> GetAetherpoolPayloads(SeString aetherpoolSeStr)
        {
            var aetherpool = string.Empty;

            foreach (var payload in aetherpoolSeStr.Payloads.Where(p => p.Type == PayloadType.RawText)) {
                var text = ((TextPayload)payload).Text;
                if (text.IndexOf('+') != -1)
                {
                    aetherpool = text.Substring(text.IndexOf('+'));
                    break;
                }
            }

            if (string.IsNullOrEmpty(aetherpool))
                aetherpool = "+0";

            var isSynced = aetherpoolSeStr.Payloads.Any(payload =>
                payload is EmphasisItalicPayload {IsEnabled: true});

            var payloads = new List<Payload> {new TextPayload(aetherpool)};
            
            if (isSynced)
            {
                payloads.Insert(0, new EmphasisItalicPayload(true));
                payloads.Insert(payloads.Count, new EmphasisItalicPayload(false));
            }
            
            if (aetherpool == "+99")
            {
                payloads.Insert(0, new UIGlowPayload(PluginInterface.Data, 501));
                payloads.Insert(payloads.Count, new UIGlowPayload(PluginInterface.Data, 0));
                payloads.Insert(0, new UIForegroundPayload(PluginInterface.Data, 500));
                payloads.Insert(payloads.Count, new UIForegroundPayload(PluginInterface.Data, 0));
            }
            
            return payloads;
        }
    }
}