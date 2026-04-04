using System;
using System.Numerics;
using System.ComponentModel;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Game.Config;
using FFXIVClientStructs.Interop;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Shield Amount in Party List")]
[TweakDescription("Show approximate shield amount in Party List.")]
[TweakAuthor("Loody")]
[TweakAutoConfig]
public unsafe class ShowShieldAmountInPartyList : UiAdjustments.SubTweak {

    private uint[] ids = [12332162, 12332163, 12332164, 12332165, 12332166, 12332167, 12332168, 12332169];
    private bool[] visible = [false, false, false, false, false, false, false, false];
    private List<(byte, uint)> actorsStats;

    private bool isManaRemoved;
    private bool removeMana = false;
    private bool revertMana = false;
    private bool showShield = false;
    private bool hideShield = false;

    private uint currentJobId = 99;

    private short nbBeforeDraw = 2;
    public enum ShieldDisplay
    {
        [Description("123.45K")]K,
        [Description("76%")]P
    }
    public class Configs : TweakConfig {
        [TweakConfigOption("Shield Display", HelpText = "Change how the shield is displayed.")]
        public ShieldDisplay ShieldDisplay = ShieldDisplay.K;

        [TweakConfigOption("Reduce mana", HelpText = "Remove last two 0 from the mana, 10000 => 100.")]
        public bool ReduceMana = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable()
    {
        actorsStats = new List<(byte, uint)>();
        removeMana = Config.ReduceMana;
        Service.Framework.Update += OnUpdate;
        Service.ClientState.ClassJobChanged += OnClassJobChanged;
        //Service.AddonLifeCycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPostRequestedUpdate);
        bool boolParty = false;
        Service.GameConfig.TryGet(UiControlOption.PartyListSoloOff, out boolParty);
        nbBeforeDraw = (short)(boolParty ? 2 : 1);
        Service.GameConfig.UiControlChanged += OnUiConfigChange;
    }

    protected override void Disable()
    {
        //Service.AddonLifeCycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPostRequestedUpdate);
        Service.Framework.Update -= OnUpdate;
        Service.GameConfig.UiControlChanged -= OnUiConfigChange;
        Service.ClientState.ClassJobChanged -= OnClassJobChanged;
        CleanShieldTweaks();
        if (isManaRemoved)
            RevertManaPart();   
    }

    [AddonPostRequestedUpdate("_PartyList")]
    private void OnPostRequestedUpdate()
    {
        if (isManaRemoved)
        {
            RemoveManaPart();
        }
        UpdateShieldNodes();
    }

    private void OnUiConfigChange(object? sender, ConfigChangeEvent e)
    {
        if (e.Option.ToString() == UiControlOption.PartyListSoloOff.ToString())
        {
            bool boolParty = false;
            Service.GameConfig.TryGet(UiControlOption.PartyListSoloOff, out boolParty);
            nbBeforeDraw = (short)(boolParty ? 2 : 1);
        }
    }

    private void OnClassJobChanged(uint classJobId)
    {
        currentJobId = classJobId;
    }

    public void OnUpdate(IFramework framework)
    {
        if (currentJobId == 99)
        {
            if (Service.PlayerState.IsLoaded)
            {
                currentJobId = Service.PlayerState.ClassJob.RowId;
            }
        }
        if (Config.ReduceMana != isManaRemoved)
        {
            ToggleManaPart();
        }
        if (removeMana)
            RemoveManaPart();
        if (revertMana)
            RevertManaPart();
        if (showShield)
            ShowShield();
        if (hideShield)
            HideShield();
    }

    private void UpdatePosShield()
    {
        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (var j = 0; j < 8; j++)
        {
            AtkTextNode* textNode = null;
            for (var i = 0; i < partyList->UldManager.NodeListCount; i++)
            {
                if (partyList->UldManager.NodeList[i] == null) continue;
                if (partyList->UldManager.NodeList[i]->NodeId == ids[j])
                {
                    textNode = (AtkTextNode*)partyList->UldManager.NodeList[i];
                    break;
                }
            }
            if (textNode == null)
                continue;
            if (isManaRemoved)
            {
                textNode->AtkResNode.SetPositionFloat(150, 65 + 40 * j);
                textNode->FontSize = 12;
                continue;
            }
            textNode->AtkResNode.SetPositionFloat(133, 65 + 40 * j);
            textNode->FontSize = 10;
        }
    }

    private void RemoveManaPart()
    {
        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (int i = 0; i < 8; i++)
            {
                var node = partyList->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(3);
                if (node == null) continue;
                node->GetAsAtkTextNode()->SetText("");

                node = partyList->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(2);
                if (node == null) continue;
                node->GetAsAtkTextNode()->SetXShort(4);
            }
        removeMana = false;
        isManaRemoved = true;
        UpdatePosShield();
    }

    private void RevertManaPart()
    {
        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (int i = 0; i < 8; i++)
        {
            if (currentJobId < 7 || currentJobId > 19)
            {
                var node = partyList->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(3);
                if (node == null) continue;
                node->GetAsAtkTextNode()->SetText("00");

                node = partyList->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(2);
                if (node == null) continue;
                node->GetAsAtkTextNode()->SetXShort(-17);
            }
        }
        revertMana = false;
        isManaRemoved = false;
        UpdatePosShield();
    }

    public void ShowShield()
    {
        showShield = false;
        UpdateShieldNodes();
    }

    public void HideShield()
    {
        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (var j = 0; j < 8; j++)
        {
            for (var i = 0; i < partyList->UldManager.NodeListCount; i++)
            {
                if (partyList->UldManager.NodeList[i] == null) continue;
                if (partyList->UldManager.NodeList[i]->NodeId == ids[j])
                {
                    partyList->UldManager.NodeList[i]->ToggleVisibility(false);
                    break;
                }
            }
        }
        hideShield = false;
        SimpleLog.Debug("hide");
    }

    public void ToggleManaPart()
    {
        if (Config.ReduceMana)
        {
            removeMana = true;
            return;
        }
        revertMana = true;

    }

    public void ToggleShield(bool show)
    {
        if (show)
            showShield = true;
        else
            hideShield = true;
    }

    private void UpdateVisibility()
    {
        var agentHUD = AgentHUD.Instance();
        short nb = agentHUD->PartyMemberCount;
        visible = [false, false, false, false, false, false, false, false];
        if (nb < nbBeforeDraw) return;
        for (int i = 0; i < nb; i++)
        {
            visible[i] = true;
        }
    }

    public unsafe void FillActors()
    {
        actorsStats.Clear();
        var agentHUD = AgentHUD.Instance();
        short nb = agentHUD->PartyMemberCount;
        if (nb < nbBeforeDraw || nb > 8)
            return;
        for (int i = 0; i < nb; i++)
        {
            byte shield;
            uint maxHP;
            try
            {
                HudPartyMember* hudPartyMember = agentHUD->PartyMembers.GetPointer(i);
                BattleChara* obj = hudPartyMember->Object;
                if (obj != null)
                {
                    var cha = obj->Character;
                    shield = cha.ShieldValue;
                    maxHP = cha.MaxHealth;
                }
                else
                {
                    shield = 0;
                    maxHP = 1;
                }
            }
            catch (Exception)
            {
                shield = 0;
                maxHP = 1;
                throw;
            }
            actorsStats.Add((shield, maxHP));
        }
    }

    private void CleanShieldTweaks()
    {
        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (var j=0; j < 8; j++)
        {
            AtkTextNode* textNode = null;
            for (var i = 0; i < partyList->UldManager.NodeListCount; i++)
            {
                if (partyList->UldManager.NodeList[i] == null) continue;
                if (partyList->UldManager.NodeList[i]->NodeId == ids[j])
                {
                    textNode = (AtkTextNode*)partyList->UldManager.NodeList[i];

                    break;
                }
            }

            if (textNode == null) return;

            if (textNode->AtkResNode.PrevSiblingNode is not null)
                textNode->AtkResNode.PrevSiblingNode->NextSiblingNode = textNode->AtkResNode.NextSiblingNode;

            if (textNode->AtkResNode.NextSiblingNode is not null)
                textNode->AtkResNode.NextSiblingNode->PrevSiblingNode = textNode->AtkResNode.PrevSiblingNode;

            partyList->UldManager.UpdateDrawNodeList();
            textNode->AtkResNode.Destroy(false);
            IMemorySpace.Free(textNode, (ulong)sizeof(AtkTextNode));
        }
    }

    private void UpdateShieldNodes()
    {
        UpdateVisibility();
        FillActors();

        var partyList = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (partyList == null) return;

        for (var j = 0; j < 8; j++)
        {
            AtkTextNode* textNode = null;
            for (var i = 0; i < partyList->UldManager.NodeListCount; i++)
            {
                if (partyList->UldManager.NodeList[i] == null) continue;
                if (partyList->UldManager.NodeList[i]->NodeId == ids[j])
                {
                    textNode = (AtkTextNode*)partyList->UldManager.NodeList[i];
                    if (!visible[j])
                    {
                        partyList->UldManager.NodeList[i]->ToggleVisibility(false);
                        continue;
                    }

                    break;
                }
            }

            if (textNode == null && !visible[j]) return;
            if (textNode == null)
            {
                var newTextNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
                if (newTextNode != null)
                {
                    var lastNode = partyList->RootNode;
                    if (lastNode == null) return;

                    textNode = newTextNode;

                    newTextNode->AtkResNode.Type = NodeType.Text;
                    newTextNode->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
                    newTextNode->AtkResNode.DrawFlags = 0;
                    if (isManaRemoved)
                        textNode->AtkResNode.SetPositionFloat(150, 65 + 40 * j);
                    else
                        textNode->AtkResNode.SetPositionFloat(133, 65 + 40 * j);
                    newTextNode->AtkResNode.SetWidth(50);
                    newTextNode->AtkResNode.SetHeight(12);

                    newTextNode->LineSpacing = 24;
                    newTextNode->AlignmentFontType = 0x15;
                    if (isManaRemoved)
                        newTextNode->FontSize = 12;
                    else
                        newTextNode->FontSize = 10;
                    newTextNode->TextFlags = TextFlags.Edge;
                    //newTextNode->TextFlags2 = 0;

                    newTextNode->AtkResNode.NodeId = ids[j];

                    newTextNode->AtkResNode.Color.A = 0xFF;
                    newTextNode->AtkResNode.Color.R = 0xFF;
                    newTextNode->AtkResNode.Color.G = 0xFF;
                    newTextNode->AtkResNode.Color.B = 0xFF;

                    if (lastNode->ChildNode != null)
                    {
                        lastNode = lastNode->ChildNode;
                        while (lastNode->PrevSiblingNode != null)
                        {
                            lastNode = lastNode->PrevSiblingNode;
                        }

                        newTextNode->AtkResNode.NextSiblingNode = lastNode;
                        newTextNode->AtkResNode.ParentNode = partyList->RootNode;
                        lastNode->PrevSiblingNode = (AtkResNode*)newTextNode;
                    }
                    else
                    {
                        lastNode->ChildNode = (AtkResNode*)newTextNode;
                        newTextNode->AtkResNode.ParentNode = lastNode;
                    }

                    textNode->TextColor.RGBA = 4294967295;
                    textNode->EdgeColor.RGBA = (UInt32)4286996785;
                    textNode->BackgroundColor.RGBA = (UInt32)0;

                    textNode->SetFont(FontType.MiedingerMed);
                    //node->SetAlignment(AlignmentType.Right);
                    //node->NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible | NodeFlags.Enabled;

                    partyList->UldManager.UpdateDrawNodeList();
                }
            }
            if (!visible[j])
            {
                textNode->AtkResNode.ToggleVisibility(false);
                continue;
            }
            textNode->AtkResNode.ToggleVisibility(true);

            textNode->AtkResNode.SetAlpha(partyList->PartyMembers[j].PartyMemberComponent->OwnerNode->Alpha_2);

            if (actorsStats[j].Item1 == 0)
            {
                textNode->SetText("");
                continue;
            }
            
            if (Config.ShieldDisplay == ShieldDisplay.K)
            {
                double shieldAmount = (float)actorsStats[j].Item1 * (float)actorsStats[j].Item2 / 100000;
                if (shieldAmount > 100)
                    shieldAmount = Math.Round(shieldAmount, 0);
                else if (shieldAmount > 10)
                    shieldAmount = Math.Round(shieldAmount, 1);
                else
                    shieldAmount = Math.Round(shieldAmount, 2);

                textNode->SetText($"{shieldAmount}K");
            }
            else //(Config.ShieldDisplay == ShieldDisplay.P)
            {
                textNode->SetText($"{actorsStats[j].Item1}%");
            }
            
        }
    }

}
