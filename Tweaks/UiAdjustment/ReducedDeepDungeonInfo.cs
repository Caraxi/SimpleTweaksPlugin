using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ReducedDeepDungeonInfo : UiAdjustments.SubTweak {
    public override string Name => "Reduced Deep Dungeon Info";
    public override string Description => "Removes the redundant infos from the deep dungeon character info.";
    protected override string Author => "Aireil";

    public override void Enable() {
        Service.Framework.Update += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable() {
        Service.Framework.Update -= OnFrameworkUpdate;
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
            gearInfoNode->ToggleVisibility(true);
            deepDungeonUnitBase->UldManager.NodeList[76]->Color.A = 255;
            deepDungeonUnitBase->UldManager.NodeList[76]->ToggleVisibility(true); // Job infos
            deepDungeonUnitBase->UldManager.NodeList[78]->ToggleVisibility(true);
            deepDungeonUnitBase->UldManager.NodeList[79]->ToggleVisibility(true);
            deepDungeonUnitBase->UldManager.NodeList[80]->ToggleVisibility(true);
            deepDungeonUnitBase->UldManager.NodeList[81]->ToggleVisibility(true);
            deepDungeonUnitBase->UldManager.NodeList[82]->ToggleVisibility(true);

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
            textNode->SetText(Service.Data.Excel.GetSheet<Addon>().GetRow(10430).Text);

            return;
        }

        gearInfoNode->ToggleVisibility(false);
        deepDungeonUnitBase->UldManager.NodeList[76]->Color.A = 0;
        deepDungeonUnitBase->UldManager.NodeList[76]->ToggleVisibility(false); // Job infos
        deepDungeonUnitBase->UldManager.NodeList[78]->ToggleVisibility(false);
        deepDungeonUnitBase->UldManager.NodeList[79]->ToggleVisibility(false);
        deepDungeonUnitBase->UldManager.NodeList[80]->ToggleVisibility(false);
        deepDungeonUnitBase->UldManager.NodeList[81]->ToggleVisibility(false);
        deepDungeonUnitBase->UldManager.NodeList[82]->ToggleVisibility(false);

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
        var armAetherpoolSeStr = Common.ReadSeString(armAetherpoolTextNode->NodeText.StringPtr);
        var armorAetherpoolSeStr = Common.ReadSeString(armorAetherpoolTextNode->NodeText.StringPtr);

        var payloads = new List<Payload>();
        payloads.AddRange(GetAetherpoolPayloads(armAetherpoolSeStr));
        payloads.Add(new TextPayload("/"));
        payloads.AddRange(GetAetherpoolPayloads(armorAetherpoolSeStr));

        textNode->SetText(new SeString(payloads).Encode());
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
            payloads.Insert(0, new UIGlowPayload(501));
            payloads.Insert(payloads.Count, new UIGlowPayload(0));
            payloads.Insert(0, new UIForegroundPayload(500));
            payloads.Insert(payloads.Count, new UIForegroundPayload(0));
        } else if (isSynced) {
            payloads.Insert(0, new UIGlowPayload(574));
            payloads.Insert(payloads.Count, new UIGlowPayload(0));
            payloads.Insert(0, new UIForegroundPayload(573));
            payloads.Insert(payloads.Count, new UIForegroundPayload(0));
        }
            
        return payloads;
    }
}