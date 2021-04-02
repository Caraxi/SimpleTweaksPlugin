using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Data.Parsing.Uld;
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
            if (deepDungeonUnitBase->ULDData.NodeList == null ||
                deepDungeonUnitBase->ULDData.NodeListCount < 84) return;

            var resNode = deepDungeonUnitBase->ULDData.NodeList[0];
            var windowCollisionNode = (AtkCollisionNode*) deepDungeonUnitBase->ULDData.NodeList[1];
            var windowNode = (AtkComponentNode*) deepDungeonUnitBase->ULDData.NodeList[2];
            var itemsEffectsInfoNode = deepDungeonUnitBase->ULDData.NodeList[3];
            var magiciteInfoNode = deepDungeonUnitBase->ULDData.NodeList[23];
            var itemsInfoNode = deepDungeonUnitBase->ULDData.NodeList[33];
            var gearInfoNode = deepDungeonUnitBase->ULDData.NodeList[71];

            var armAetherpoolNode = (AtkComponentNode*) deepDungeonUnitBase->ULDData.NodeList[73];
            var armAetherpoolTextNode = (AtkTextNode*) armAetherpoolNode->Component->ULDData.NodeList[1];
            var armorAetherpoolNode = (AtkComponentNode*) deepDungeonUnitBase->ULDData.NodeList[72];
            var armorAetherpoolTextNode = (AtkTextNode*) armorAetherpoolNode->Component->ULDData.NodeList[1];
            var textNode = (AtkTextNode*) deepDungeonUnitBase->ULDData.NodeList[77]; // "To next level:" node

            var isHoh = magiciteInfoNode->IsVisible;

            if (reset) {
                UiHelper.Show(gearInfoNode);
                deepDungeonUnitBase->ULDData.NodeList[76]->Color.A = 255;
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[76]); // Job infos
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[78]);
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[79]);
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[80]);
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[81]);
                UiHelper.Show(deepDungeonUnitBase->ULDData.NodeList[82]);

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
            deepDungeonUnitBase->ULDData.NodeList[76]->Color.A = 0;
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[76]); // Job infos
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[78]);
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[79]);
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[80]);
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[81]);
            UiHelper.Hide(deepDungeonUnitBase->ULDData.NodeList[82]);

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
                case var _ when limiter > 0:
                    limiter--;
                    return;
                // Burst when the window is created
                case var _ when limiter < 0:
                    limiter++;
                    break;
                case var _ when limiter == 0:
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

            var n = windowNode->Component->ULDData.RootNode;
            UiHelper.SetSize(windowNode, width, height);  // Window
            UiHelper.SetSize(n, width, height);  // Collision
            n = n->PrevSiblingNode->PrevSiblingNode;
            UiHelper.SetSize(n, width - 2, height - 2); // Background
            n = n->PrevSiblingNode;
            UiHelper.SetSize(n, width - 2, height - 2); // Focused Border

            windowNode->AtkResNode.Flags_2 |= 0x1;
        }

        private static IEnumerable<Payload> GetAetherpoolPayloads(SeString aetherpoolSeStr)
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

            var payloads = new List<Payload>();

            if (aetherpool == "+99")
            {
                payloads.Add(new RawPayload(new byte[] { 2, 72, 4, 242, 1, 244, 3 })); // UIForeground
                payloads.Add(new RawPayload(new byte[] { 2, 73, 4, 242, 1, 245, 3 })); // UIGlow
                payloads.Add(new TextPayload(aetherpool));
                payloads.Add(new RawPayload(new byte[] { 2, 73, 2, 1, 3 })); // UIGlow
                payloads.Add(new RawPayload(new byte[] { 2, 72, 2, 1, 3 })); // UIForeground
            }
            else
                payloads.Add(new TextPayload(aetherpool));

            return payloads;
        }
    }
}