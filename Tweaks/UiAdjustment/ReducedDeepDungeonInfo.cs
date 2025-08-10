using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

[TweakName("Reduced Deep Dungeon Info")]
[TweakDescription("Removes redundant information from the deep dungeon character info.")]
[TweakAuthor("Aireil")]
public unsafe class ReducedDeepDungeonInfo : UiAdjustments.SubTweak {
    protected override void Enable() {
        limiter = -10;
        UpdateDeepDungeonStatus(Common.GetUnitBase("DeepDungeonStatus"), false);
    }

    protected override void Disable() {
        UpdateDeepDungeonStatus(Common.GetUnitBase("DeepDungeonStatus"), true);
    }
    
    [AddonPreDraw("DeepDungeonStatus")]
    private void UpdateDeepDungeonStatus(AtkUnitBase* unitBase) {
        UpdateDeepDungeonStatus(unitBase, false);
    }

    [AddonPostSetup("DeepDungeonStatus")]
    private void SetupDeepDungeonStatus() {
        limiter = -10;
    }
    
    private int limiter;

    private void UpdateDeepDungeonStatus(AtkUnitBase* deepDungeonUnitBase, bool reset) {
        if (deepDungeonUnitBase == null) return;
        
        if (deepDungeonUnitBase->UldManager.NodeList == null ||
            deepDungeonUnitBase->UldManager.NodeListCount < 84) return;

        var resNode = deepDungeonUnitBase->UldManager.NodeList[0];
        var guideNode = deepDungeonUnitBase->UldManager.SearchNodeById(3);
        var windowCollisionNode = (AtkCollisionNode*) deepDungeonUnitBase->UldManager.NodeList[1];
        var windowNode = (AtkComponentNode*) deepDungeonUnitBase->UldManager.NodeList[4];
        var itemsEffectsInfoNode = deepDungeonUnitBase->GetNodeById(64);
        var magiciteInfoNode = deepDungeonUnitBase->GetNodeById(54);
        var itemsInfoNode = deepDungeonUnitBase->GetNodeById(16);
        var gearInfoNode = deepDungeonUnitBase->GetNodeById(12);

        var armAetherpoolNode = deepDungeonUnitBase->GetComponentNodeById(14);
        var armAetherpoolTextNode = armAetherpoolNode->Component->GetTextNodeById(3);
        var armorAetherpoolNode = deepDungeonUnitBase->GetComponentNodeById(15);
        var armorAetherpoolTextNode = armorAetherpoolNode->Component->GetTextNodeById(3);
        
        var textNode = deepDungeonUnitBase->GetTextNodeById(10);

        var isHoh = magiciteInfoNode->IsVisible();

        if (reset) {
            gearInfoNode->ToggleVisibility(true);
            guideNode->ToggleVisibility(true);
            deepDungeonUnitBase->GetNodeById(5)->ToggleVisibility(true); // Job infos
            deepDungeonUnitBase->GetNodeById(6)->ToggleVisibility(true);
            deepDungeonUnitBase->GetNodeById(7)->ToggleVisibility(true);
            deepDungeonUnitBase->GetNodeById(8)->ToggleVisibility(true);
            deepDungeonUnitBase->GetNodeById(9)->ToggleVisibility(true);
            deepDungeonUnitBase->GetNodeById(11)->ToggleVisibility(true);

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
            textNode->SetText(Service.Data.Excel.GetSheet<Addon>()!.GetRow(10430)!.Text);

            return;
        }

        gearInfoNode->ToggleVisibility(false);
        guideNode->ToggleVisibility(false);
        deepDungeonUnitBase->GetNodeById(5)->ToggleVisibility(false); // Job infos
        deepDungeonUnitBase->GetNodeById(6)->ToggleVisibility(false);
        deepDungeonUnitBase->GetNodeById(7)->ToggleVisibility(false);
        deepDungeonUnitBase->GetNodeById(8)->ToggleVisibility(false);
        deepDungeonUnitBase->GetNodeById(9)->ToggleVisibility(false);
        deepDungeonUnitBase->GetNodeById(11)->ToggleVisibility(false);

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

        textNode->SetText(new SeString(payloads).EncodeWithNullTerminator());
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

        windowNode->AtkResNode.DrawFlags |= 0x1;
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
