using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Party UI adjustments")]
[TweakDescription("Party UI adjustments.")]
[TweakAuthor("wozaiha")]
[TweakAutoConfig]
public unsafe class PartyUIAdjustments : UiAdjustments.SubTweak
{
    private AddonPartyList* _partyListUi;

    private bool Shifted;
    private static int* DataArray => RaptureAtkModule.Instance()->GetNumberArrayData(4)->IntArray;

    public static Configs Config { get; private set; }

    public class Configs : TweakConfig
    {
        [TweakConfigOption("Move sheld node a bit down", 0)] public bool ShieldShift = false;

        [TweakConfigOption("Show HP as percentage", 2)] public bool HpPercent;

        [TweakConfigOption("Show Shield value (approximately) as MP", 3)] public bool MpShield;

        public bool ShouldShowShieldPercentage() => MpShield;

        [TweakConfigOption("Show shield percentage", 4, SameLine = true, ConditionalDisplay = true)]
        public bool ShieldPercentage;
    }

    protected override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();
    }

    [AddonPostSetup(["_PartyList"])]
    private void PostSetup()
    {
        _partyListUi = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        Shifted = false;
        SimpleLog.Debug($"PartyListUI = {(nint)_partyListUi:X}");
    }

    protected override void ConfigChanged()
    {
        if (!Config.ShieldShift) UnShift();

        if (!Config.MpShield)
            for (var i = 0; i < 8; i++)
            {
                var mpNode = _partyListUi->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(3)->GetAsAtkTextNode();
                mpNode->TextColor = new ByteColor() { RGBA = ColorHelpers.RgbaVector4ToUint(ImGuiColors.DalamudWhite) };
                mpNode->FontSize = 10;
                mpNode->AlignmentType = AlignmentType.Right;
            }

        base.ConfigChanged();
    }

    [AddonPreDraw(["_PartyList"])]
    private void Update()
    {
        if (_partyListUi is null)
        {
            PostSetup();
            return;
        }

        if (Config.ShieldShift && !Shifted) Shift();

        ChangeNumber();
    }

    private void ChangeNumber()
    {
        for (var i = 0; i < _partyListUi->MemberCount; i++)
        {
            if (Config.HpPercent)
            {
                var hpNode =
                    _partyListUi->PartyMembers[i].HPGaugeComponent->UldManager.SearchNodeById(2)->GetAsAtkTextNode();
                var currentHp = DataArray[ArrayCount(i, 13)];
                var maxHp = DataArray[ArrayCount(i, 14)];
                if (maxHp == 0) continue;

                hpNode->SetText($"{currentHp * 100 / maxHp}" + "%");
            }

            if (Config.MpShield)
            {
                var mpNodeToHide =
                    _partyListUi->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(2)->GetAsAtkTextNode();
                var mpNode = _partyListUi->PartyMembers[i].MPGaugeBar->UldManager.SearchNodeById(3)->GetAsAtkTextNode();
                var shield = DataArray[ArrayCount(i, 15)];
                var maxHp = DataArray[ArrayCount(i, 14)];
                if (maxHp == 0) continue;

                mpNodeToHide->ToggleVisibility(false);
                mpNode->SetText(Config.ShieldPercentage ? shield + "%" : (shield * maxHp / 100).ToString());
                mpNode->TextColor = new ByteColor()
                    { RGBA = ColorHelpers.RgbaVector4ToUint(ImGuiColors.DalamudYellow) };
                mpNode->AlignmentType = AlignmentType.Left;
                mpNode->FontSize = 12;
            }
        }
    }

    private int ArrayCount(int index, int offset)
    {
        return index * 42 + offset;
    }

    private void Shift()
    {
        foreach (var member in _partyListUi->PartyMembers)
            for (uint i = 2; i <= 5; i++)
            {
                var node = member.HPGaugeBar->UldManager.SearchNodeById(i);
                if (i == 2 && node->Y == 17) break;
                switch (i)
                {
                    case 2:
                        node->SetPositionShort(90, 17);
                        break;
                    case 3 or 4:
                        node->SetPositionShort(0, 0);
                        break;
                    case 5:
                        node->SetPositionShort(0, 16);
                        break;
                }

                Shifted = true;
            }
    }

    private void UnShift()
    {
        if (_partyListUi is null) return;

        foreach (var member in _partyListUi->PartyMembers)
            for (uint i = 2; i <= 5; i++)
            {
                var node = member.HPGaugeBar->UldManager.SearchNodeById(i);
                if (i == 2 && node->Y == 9) return;
                switch (i)
                {
                    case 2:
                        node->SetPositionShort(90, 9);
                        break;
                    case 3 or 4:
                        node->SetPositionShort(0, -8);
                        break;
                    case 5:
                        node->SetPositionShort(0, 8);
                        break;
                }

                Shifted = false;
            }
    }
}