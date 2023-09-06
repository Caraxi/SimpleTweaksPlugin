using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Improved Duty Finder Settings")]
[TweakDescription("Turn the duty finder settings into buttons.")]
[TweakAuthor("Aireil")]
[TweakReleaseVersion("1.8.3.0")]
[Changelog("1.8.4.0", "Fixed UI displaying on wrong monitor in specific circumstances.")]
[Changelog("1.8.7.2", "Fixed tweak not working in 6.4")]
[Changelog(UnreleasedVersion, "Rewritten to use native UI")]
public unsafe class ImprovedDutyFinderSettings : UiAdjustments.SubTweak {
    private static ImprovedDutyFinderSettings _tweak;

    private record DutyFinderSettingDisplay(DutyFinderSetting Setting) {
        public DutyFinderSettingDisplay(DutyFinderSetting setting, int icon, uint tooltip) : this(setting) {
            GetIcon = () => icon;
            GetTooltip = () => tooltip;
        }

        public Func<int> GetIcon { get; init; }
        public Func<uint> GetTooltip { get; init; }

        private SimpleEvent eventManager;

        private void HideTooltip(AtkUnitBase* unitBase) {
            AtkStage.GetSingleton()->TooltipManager.HideTooltip(unitBase->ID);
        }

        private void ShowTooltip(AtkUnitBase* unitBase, AtkResNode* node) {
            var tooltipId = GetTooltip();
            var tooltip = Service.Data.GetExcelSheet<Addon>()?.GetRow(tooltipId)?.Text.ToDalamudString()?.TextValue ?? $"{Setting}";
            AtkStage.GetSingleton()->TooltipManager.ShowTooltip(unitBase->ID, node, tooltip);
        }

        public SimpleEvent Event {
            get {
                if (eventManager != null) return eventManager;
                eventManager = new SimpleEvent((type, unitBase, node) => {
                    if (type == AtkEventType.MouseOver) {
                        ShowTooltip(unitBase, node);
                        Common.ForceMouseCursor(AtkCursor.CursorType.Clickable);
                        return;
                    }

                    if (type == AtkEventType.MouseOut) {
                        HideTooltip(unitBase);
                        Common.UnforceMouseCursor();
                        return;
                    }

                    if (Service.Condition[ConditionFlag.BoundToDuty97]) return;
                    if (type != AtkEventType.MouseClick) return;

                    _tweak.ToggleSetting(Setting);

                    if (Setting == DutyFinderSetting.LootRule) {
                        HideTooltip(unitBase);
                        ShowTooltip(unitBase, node);
                    }
                });
                return eventManager;
            }
        }
    };

    private readonly List<DutyFinderSettingDisplay> dutyFinderSettingIcons = new() {
        new DutyFinderSettingDisplay(DutyFinderSetting.JoinPartyInProgress, 60644, 2519),
        new DutyFinderSettingDisplay(DutyFinderSetting.UnrestrictedParty, 60641, 10008),
        new DutyFinderSettingDisplay(DutyFinderSetting.LevelSync, 60649, 12696),
        new DutyFinderSettingDisplay(DutyFinderSetting.MinimumIl, 60642, 10010),
        new DutyFinderSettingDisplay(DutyFinderSetting.SilenceEcho, 60647, 12691),
        new DutyFinderSettingDisplay(DutyFinderSetting.ExplorerMode, 60648, 13038),
        new DutyFinderSettingDisplay(DutyFinderSetting.LimitedLevelingRoulette, 60640, 13030),
        new DutyFinderSettingDisplay(DutyFinderSetting.LootRule) {
            GetIcon = () => {
                return GetCurrentSettingValue(DutyFinderSetting.LootRule) switch {
                    0 => 60645,
                    1 => 60645,
                    2 => 60646,
                    _ => 0,
                };
            },
            GetTooltip = () => {
                return GetCurrentSettingValue(DutyFinderSetting.LootRule) switch {
                    0 => 10022,
                    1 => 10023,
                    2 => 10024,
                    _ => 0,
                };
            }
        },
    };

    private readonly List<DutyFinderSettingDisplay> languageButtons = new() {
        new DutyFinderSettingDisplay(DutyFinderSetting.Ja, 0, 10),
        new DutyFinderSettingDisplay(DutyFinderSetting.En, 0, 11),
        new DutyFinderSettingDisplay(DutyFinderSetting.De, 0, 12),
        new DutyFinderSettingDisplay(DutyFinderSetting.Fr, 0, 13),
    };

    [AddonSetup("ContentsFinder", "RaidFinder")]
    protected void ContentsFinderSetup(AtkUnitBase* addonContentsFinder) {
        SetupAddon(addonContentsFinder);
    }

    [AddonFinalize("ContentsFinder", "RaidFinder")]
    protected void ContentsFinderFinalize(AtkUnitBase* addonContentsFinder) {
        ResetAddon(addonContentsFinder);
    }

    private void SetupAddon(AtkUnitBase* unitBase) {
        var defaultContainer = unitBase->GetNodeById(6);
        if (defaultContainer == null) return;
        defaultContainer->ToggleVisibility(false);

        var container = IMemorySpace.GetUISpace()->Create<AtkResNode>();
        container->SetWidth(defaultContainer->GetWidth());
        container->SetHeight(defaultContainer->GetHeight());
        container->SetPositionFloat(defaultContainer->GetX(), defaultContainer->GetY());
        container->SetScale(1, 1);
        container->NodeID = CustomNodes.Get($"{nameof(ImprovedDutyFinderSettings)}_Container");
        container->Type = NodeType.Res;
        container->ToggleVisibility(true);
        UiHelper.LinkNodeAfterTargetNode(container, unitBase, defaultContainer);

        for (var i = 0; i < dutyFinderSettingIcons.Count; i++) {
            var settingDetail = dutyFinderSettingIcons[i];

            var basedOn = unitBase->GetNodeById(7 + (uint)i);
            if (basedOn == null) continue;

            var imgNode = UiHelper.MakeImageNode(CustomNodes.Get($"{nameof(ImprovedDutyFinderSettings)}_Icon_{settingDetail.Setting}"), new UiHelper.PartInfo(0, 0, 24, 24));
            UiHelper.LinkNodeAtEnd(imgNode, container, unitBase);

            imgNode->AtkResNode.SetPositionFloat(basedOn->GetX(), basedOn->GetY());
            imgNode->AtkResNode.SetWidth(basedOn->GetWidth());
            imgNode->AtkResNode.SetHeight(basedOn->GetHeight());

            imgNode->AtkResNode.NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;

            settingDetail.Event.Add(unitBase, &imgNode->AtkResNode, AtkEventType.MouseClick);
            settingDetail.Event.Add(unitBase, &imgNode->AtkResNode, AtkEventType.MouseOver);
            settingDetail.Event.Add(unitBase, &imgNode->AtkResNode, AtkEventType.MouseOut);
        }

        for (var i = 0; i < languageButtons.Count; i++) {
            var settingDetail = languageButtons[i];

            var node = unitBase->GetNodeById(17 + (uint)i);
            if (node == null) continue;

            node->NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
            settingDetail.Event.Add(unitBase, node, AtkEventType.MouseClick);
            settingDetail.Event.Add(unitBase, node, AtkEventType.MouseOver);
            settingDetail.Event.Add(unitBase, node, AtkEventType.MouseOut);
        }

        unitBase->UpdateCollisionNodeList(false);
        frameworkTicksSinceUpdate = 0;
        Common.FrameworkUpdate -= UpdateIcons;
        Common.FrameworkUpdate += UpdateIcons;
        UpdateIcons(unitBase);
    }

    private int frameworkTicksSinceUpdate = 0;
    
    private void UpdateIcons() {
        if (Common.GetUnitBase("ContentsFinder", out var unitBase)) UpdateIcons(unitBase); 
        if (Common.GetUnitBase("RaidFinder", out var raidFinder)) UpdateIcons(raidFinder);
        if (frameworkTicksSinceUpdate++ > 5) Common.FrameworkUpdate -= UpdateIcons;
    }
    
    private void UpdateIcons(AtkUnitBase* unitBase) {
        if (unitBase == null) return;
        frameworkTicksSinceUpdate = 0;
        for (var i = 0; i < dutyFinderSettingIcons.Count; i++) {
            var settingDetail = dutyFinderSettingIcons[i];
            var nodeId = CustomNodes.Get($"{nameof(ImprovedDutyFinderSettings)}_Icon_{settingDetail.Setting}");
            var imgNode = Common.GetNodeByID<AtkImageNode>(&unitBase->UldManager, nodeId, NodeType.Image);
            if (imgNode == null) continue;
            
            var icon = settingDetail.GetIcon();
            // Game gets weird sometimes loading Icons using the specific icon function...
            imgNode->LoadTexture($"ui/icon/{icon / 5000 * 5000:000000}/{icon:000000}.tex");
            imgNode->AtkResNode.ToggleVisibility(true);
            var value = GetCurrentSettingValue(settingDetail.Setting);

            var isSettingDisabled = (settingDetail.Setting == DutyFinderSetting.LevelSync && GetCurrentSettingValue(DutyFinderSetting.UnrestrictedParty) == 0);

            if (isSettingDisabled) {
                imgNode->AtkResNode.Color.A = (byte)(value != 0 ? 255 : 180);
                imgNode->AtkResNode.Alpha_2 = (byte)(value != 0 ? 255 : 180);

                imgNode->AtkResNode.MultiplyRed = 5;
                imgNode->AtkResNode.MultiplyGreen = 5;
                imgNode->AtkResNode.MultiplyBlue = 5;
                imgNode->AtkResNode.AddRed = 120;
                imgNode->AtkResNode.AddGreen = 120;
                imgNode->AtkResNode.AddBlue = 120;
            } else {
                imgNode->AtkResNode.Color.A = (byte)(value != 0 ? 255 : 127);
                imgNode->AtkResNode.Alpha_2 = (byte)(value != 0 ? 255 : 127);

                imgNode->AtkResNode.AddBlue = 0;
                imgNode->AtkResNode.AddGreen = 0;
                imgNode->AtkResNode.AddRed = 0;
                imgNode->AtkResNode.MultiplyRed = 100;
                imgNode->AtkResNode.MultiplyGreen = 100;
                imgNode->AtkResNode.MultiplyBlue = 100;
            }
        }
    }

    private void ResetAddon(AtkUnitBase* unitBase) {
        var vanillaIconContainer = unitBase->GetNodeById(6);
        if (vanillaIconContainer == null) return;
        vanillaIconContainer->ToggleVisibility(true);
        var container = Common.GetNodeByID(&unitBase->UldManager, CustomNodes.Get($"{nameof(ImprovedDutyFinderSettings)}_Container"));

        for (var i = 0; i < dutyFinderSettingIcons.Count; i++) {
            var settingDetail = dutyFinderSettingIcons[i];
            var imgNode = Common.GetNodeByID<AtkImageNode>(&unitBase->UldManager, CustomNodes.Get($"{nameof(ImprovedDutyFinderSettings)}_Icon_{settingDetail.Setting}"), NodeType.Image);
            if (imgNode == null) continue;

            UiHelper.UnlinkAndFreeImageNode(imgNode, unitBase);
        }

        for (var i = 0; i < languageButtons.Count; i++) {
            var settingDetail = languageButtons[i];

            var node = unitBase->GetNodeById(17 + (uint)i);
            if (node == null) continue;

            settingDetail.Event.Remove(unitBase, node, AtkEventType.MouseClick);
            settingDetail.Event.Remove(unitBase, node, AtkEventType.MouseOver);
            settingDetail.Event.Remove(unitBase, node, AtkEventType.MouseOut);
        }

        if (container == null) return;
        UiHelper.UnlinkNode(container, unitBase);
        container->Destroy(true);

        unitBase->UldManager.UpdateDrawNodeList();
        unitBase->UpdateCollisionNodeList(false);
    }

    protected override void Enable() {
        _tweak = this;
        if (Common.GetUnitBase("ContentsFinder", out var unitBase)) {
            SetupAddon(unitBase);
        } else if (Common.GetUnitBase("RaidFinder", out var raidFinder)) {
            SetupAddon(raidFinder);
        }
    }

    protected override void Disable() {
        if (Common.GetUnitBase("ContentsFinder", out var unitBase)) {
            ResetAddon(unitBase);
        } else if (Common.GetUnitBase("RaidFinder", out var raidFinder)) {
            ResetAddon(raidFinder);
        }

        Common.FrameworkUpdate -= UpdateIcons;
    }

    private enum DutyFinderSetting {
        Ja = 0,
        En = 1,
        De = 2,
        Fr = 3,
        LootRule = 4,
        JoinPartyInProgress = 5,
        UnrestrictedParty = 6,
        LevelSync = 7,
        MinimumIl = 8,
        SilenceEcho = 9,
        ExplorerMode = 10,
        LimitedLevelingRoulette = 11,
    }
    
    [Signature("E8 ?? ?? ?? ?? 48 8B 07 33 F6")]
    private static delegate* unmanaged<byte*, nint, void> _setContentsFinderSettings;

    private static byte GetCurrentSettingValue(DutyFinderSetting dutyFinderSetting) {
        var contentsFinder = ContentsFinder.Instance();
        return dutyFinderSetting switch {
            DutyFinderSetting.Ja => (byte)Service.GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeJA"),
            DutyFinderSetting.En => (byte)Service.GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeEN"),
            DutyFinderSetting.De => (byte)Service.GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeDE"),
            DutyFinderSetting.Fr => (byte)Service.GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeFR"),
            DutyFinderSetting.LootRule => (byte)contentsFinder->LootRules,
            DutyFinderSetting.JoinPartyInProgress => (byte)Service.GameConfig.UiConfig.GetUInt("ContentsFinderSupplyEnable"),
            DutyFinderSetting.UnrestrictedParty => *(byte*)&contentsFinder->IsUnrestrictedParty,
            DutyFinderSetting.LevelSync => *(byte*)&contentsFinder->IsLevelSync,
            DutyFinderSetting.MinimumIl => *(byte*)&contentsFinder->IsMinimalIL,
            DutyFinderSetting.SilenceEcho => *(byte*)&contentsFinder->IsSilenceEcho,
            DutyFinderSetting.ExplorerMode => *(byte*)&contentsFinder->IsExplorerMode,
            DutyFinderSetting.LimitedLevelingRoulette => *(byte*)&contentsFinder->IsLimitedLevelingRoulette,
            _ => 0,
        };
    }

    private void ToggleSetting(DutyFinderSetting setting) {
        // block setting change if queued for a duty
        if (Service.Condition[ConditionFlag.BoundToDuty97]) {
            return;
        }

        // always need at least one language enabled
        if (setting is DutyFinderSetting.Ja or DutyFinderSetting.En or DutyFinderSetting.De or DutyFinderSetting.Fr) {
            var nbEnabledLanguages = GetCurrentSettingValue(DutyFinderSetting.Ja) + GetCurrentSettingValue(DutyFinderSetting.En) + GetCurrentSettingValue(DutyFinderSetting.De) + GetCurrentSettingValue(DutyFinderSetting.Fr);
            if (nbEnabledLanguages == 1 && GetCurrentSettingValue(setting) == 1) {
                return;
            }
        }

        var array = GetCurrentSettingArray();
        if (array == null) {
            return;
        }

        byte newValue;
        if (setting == DutyFinderSetting.LootRule) {
            newValue = (byte)((array[(int)setting] + 1) % 3);
        } else {
            newValue = (byte)(array[(int)setting] == 0 ? 1 : 0);
        }

        array[(int)setting] = newValue;

        if (!IsSettingArrayValid(array)) {
            SimpleLog.Error("Tweak appears to be broken.");
            return;
        }

        fixed (byte* arrayPtr = array) {
            _setContentsFinderSettings(arrayPtr, (nint)Framework.Instance()->GetUiModule());
        }
    }

    // array used in setContentsFinderSettings
    private static byte[] GetCurrentSettingArray() {
        var array = new byte[27];
        var nbSettings = Enum.GetValues<DutyFinderSetting>().Length;
        for (var i = 0; i < nbSettings; i++) {
            array[i] = GetCurrentSettingValue((DutyFinderSetting)i);
            array[i + nbSettings] = GetCurrentSettingValue((DutyFinderSetting)i); // prev value to print in chat when changed
        }

        array[26] = 1; // has changed

        return array;
    }

    private static bool IsSettingArrayValid(IReadOnlyList<byte> array) {
        var isArrayValid = true;
        var nbSettings = Enum.GetValues<DutyFinderSetting>().Length; // % for previous values
        for (var index = 0; index < array.Count; index++) {
            if ((index % nbSettings != (int)DutyFinderSetting.LootRule && array[index] != 0 && array[index] != 1) || (array[index] != 0 && array[index] != 1 && array[index] != 2)) {
                isArrayValid = false;
                SimpleLog.Error($"Invalid setting value ({array[index]}) for: {(DutyFinderSetting)(index % nbSettings)}");
            }
        }

        // duty server would reject any request without language set
        if (array[(int)DutyFinderSetting.Ja] == 0 && array[(int)DutyFinderSetting.En] == 0 && array[(int)DutyFinderSetting.De] == 0 && array[(int)DutyFinderSetting.Fr] == 0) {
            isArrayValid = false;
            SimpleLog.Error("No language selected, this is impossible.");
        }

        return isArrayValid;
    }
}
