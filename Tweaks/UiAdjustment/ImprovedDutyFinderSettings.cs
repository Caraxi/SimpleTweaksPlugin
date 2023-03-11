using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

using LootRule = ContentsFinder.LootRule;

public unsafe class ImprovedDutyFinderSettings : UiAdjustments.SubTweak {
    public override string Name => "Improved Duty Finder Settings";
    public override string Description => "Turn the duty finder settings into buttons.";
    protected override string Author => "Aireil";

    public override void Setup() {
        AddChangelogNewTweak("1.8.3.0");
        AddChangelog("1.8.4.0", "Fixed UI displaying on wrong monitor in specific circumstances.").Author("Aireil");
        base.Setup();
    }

    public override void Enable() {
        setContentsFinderSettings = (delegate* unmanaged<byte*, nint, void>) Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 07 33 F6");
        this.LoadIcons();
        Service.PluginInterface.UiBuilder.Draw += this.OnDraw;
        base.Enable();
    }

    public override void Disable() {
        Service.PluginInterface.UiBuilder.Draw -= this.OnDraw;
        this.DisposeIcons();
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("ContentsFinder");
        if (addon == null) {
            addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("RaidFinder");
        }

        if (addon != null) {
            var buttons = addon->UldManager.SearchNodeById(6);
            if (buttons != null) {
                buttons->ToggleVisibility(true);
            }
        }

        base.Disable();
    }

    private delegate* unmanaged<byte*, nint, void> setContentsFinderSettings;
    private volatile bool iconsReady;
    private Dictionary<uint, TextureWrap> icons;
    private readonly List<DutyFinderSetting> dutyFinderSettingOrder = new() {
        DutyFinderSetting.JoinPartyInProgress,
        DutyFinderSetting.UnrestrictedParty,
        DutyFinderSetting.LevelSync,
        DutyFinderSetting.MinimumIl,
        DutyFinderSetting.SilenceEcho,
        DutyFinderSetting.ExplorerMode,
        DutyFinderSetting.LimitedLevelingRoulette,
        DutyFinderSetting.LootRule,
        DutyFinderSetting.Ja,
        DutyFinderSetting.En,
        DutyFinderSetting.De,
        DutyFinderSetting.Fr,
    };

    // values are matching the index in the array passed to setContentsFinderSettings
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

    private static uint GetIconId(DutyFinderSetting dutyFinderSetting, LootRule lootRule = LootRule.Normal) {
        return dutyFinderSetting switch {
            DutyFinderSetting.LootRule => lootRule switch {
                LootRule.Normal => 60003,
                LootRule.GreedOnly => 60645,
                LootRule.Lootmaster => 60646,
                _ => 0,
            },
            DutyFinderSetting.JoinPartyInProgress => 60644,
            DutyFinderSetting.UnrestrictedParty => 60641,
            DutyFinderSetting.LevelSync => 60649,
            DutyFinderSetting.MinimumIl => 60642,
            DutyFinderSetting.SilenceEcho => 60647,
            DutyFinderSetting.ExplorerMode => 60648,
            DutyFinderSetting.LimitedLevelingRoulette => 60640,
            _ => 0,
        };
    }

    private static byte GetCurrentSettingValue(DutyFinderSetting dutyFinderSetting) {
        var contentsFinder = UIState.Instance()->ContentsFinder;
        return dutyFinderSetting switch {
            DutyFinderSetting.Ja => (byte)GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeJA"),
            DutyFinderSetting.En => (byte)GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeEN"),
            DutyFinderSetting.De => (byte)GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeDE"),
            DutyFinderSetting.Fr => (byte)GameConfig.UiConfig.GetUInt("ContentsFinderUseLangTypeFR"),
            DutyFinderSetting.LootRule => (byte)contentsFinder.LootRules,
            DutyFinderSetting.JoinPartyInProgress => (byte)GameConfig.UiConfig.GetUInt("ContentsFinderSupplyEnable"),
            DutyFinderSetting.UnrestrictedParty => *(byte*)&contentsFinder.IsUnrestrictedParty,
            DutyFinderSetting.LevelSync => *(byte*)&contentsFinder.IsLevelSync,
            DutyFinderSetting.MinimumIl => *(byte*)&contentsFinder.IsMinimalIL,
            DutyFinderSetting.SilenceEcho => *(byte*)&contentsFinder.IsSilenceEcho,
            DutyFinderSetting.ExplorerMode => *(byte*)&contentsFinder.IsExplorerMode,
            DutyFinderSetting.LimitedLevelingRoulette => *(byte*)&contentsFinder.IsLimitedLevelingRoulette,
            _ => 0,
        };
    }

    private static string GetTooltip(DutyFinderSetting dutyFinderSetting, LootRule lootRule = LootRule.Normal) {
        var addonSheet = Service.Data.Excel.GetSheet<Addon>();
        return dutyFinderSetting switch {
            DutyFinderSetting.Ja => addonSheet?.GetRow(10)?.Text.ToDalamudString().ToString() ?? "Japanese",
            DutyFinderSetting.En => addonSheet?.GetRow(11)?.Text?.ToDalamudString().ToString() ?? "English",
            DutyFinderSetting.De => addonSheet?.GetRow(12)?.Text?.ToDalamudString().ToString() ?? "German",
            DutyFinderSetting.Fr => addonSheet?.GetRow(13)?.Text?.ToDalamudString().ToString() ?? "French",
            DutyFinderSetting.LootRule => lootRule switch
            {
                LootRule.Normal => addonSheet?.GetRow(10022)?.Text?.ToDalamudString().ToString() ?? "Loot Rule: Normal",
                LootRule.GreedOnly => addonSheet?.GetRow(10023)?.Text?.ToDalamudString().ToString() ?? "Loot Rule: Greed Only",
                LootRule.Lootmaster => addonSheet?.GetRow(10024)?.Text?.ToDalamudString().ToString() ?? "Loot Rule: Lootmaster",
                _ => "Unknown Loot Rule",
            },
            DutyFinderSetting.JoinPartyInProgress => addonSheet?.GetRow(2519)?.Text?.ToDalamudString().ToString() ?? "Join Party in Progress",
            DutyFinderSetting.UnrestrictedParty => addonSheet?.GetRow(10008)?.Text?.ToDalamudString().ToString() ?? "Unrestricted Party",
            DutyFinderSetting.LevelSync => addonSheet?.GetRow(12696)?.Text?.ToDalamudString().ToString() ?? "Level Sync",
            DutyFinderSetting.MinimumIl => addonSheet?.GetRow(10010)?.Text?.ToDalamudString().ToString() ?? "Minimum IL",
            DutyFinderSetting.SilenceEcho => addonSheet?.GetRow(12691)?.Text?.ToDalamudString().ToString() ?? "Silence Echo",
            DutyFinderSetting.ExplorerMode => addonSheet?.GetRow(13038)?.Text?.ToDalamudString().ToString() ?? "Explorer Mode",
            DutyFinderSetting.LimitedLevelingRoulette => addonSheet?.GetRow(13030)?.Text?.ToDalamudString().ToString() ?? "Limited Leveling Roulette",
            _ => "Unknown tooltip",
        };
    }

    private void LoadIcons() {
        this.icons = new Dictionary<uint, TextureWrap>();
        var iconIdsToLoad = new List<uint>();

        foreach (var setting in Enum.GetValues<DutyFinderSetting>()) {
            if (setting == DutyFinderSetting.LootRule) {
                iconIdsToLoad.AddRange(Enum.GetValues<LootRule>().Select(lootRule => GetIconId(setting, lootRule)));
            } else {
                iconIdsToLoad.Add(GetIconId(setting));
            }
        }

        iconIdsToLoad.RemoveAll(id => id == 0);

        Task.Run(() => {
            foreach (var id in iconIdsToLoad) {
                var icon = GetIconTextureWrap(id);
                if (icon != null) {
                    this.icons[id] = icon;
                } else {
                    this.DisposeIcons();
                    SimpleLog.Error("Failed to load icons.");
                    break;
                }
            }

            this.iconsReady = true;
        });
    }

    private static TextureWrap GetIconTextureWrap(uint id) {
        try {
            var iconPath = $"ui/icon/060000/0{id}_hr1.tex";
            var iconTex = Service.Data.GetFile<TexFile>(iconPath);
            if (iconTex != null) {
                var tex = Service.PluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);
                if (tex.ImGuiHandle != nint.Zero) {
                    return tex;
                }
            }
        }
        catch (Exception ex) {
            SimpleLog.Error(ex);
        }

        return null;
    }

    private void DisposeIcons() {
        if (this.icons != null) {
            foreach (var (_, icon) in this.icons) {
                icon.Dispose();
            }

            this.icons.Clear();
        }
    }

    private TextureWrap GetIcon(DutyFinderSetting dutyFinderSetting, LootRule lootRule = LootRule.Normal) {
        if (this.iconsReady && this.icons.TryGetValue(GetIconId(dutyFinderSetting, lootRule), out var iconTex))
            return iconTex;

        return null;
    }

    private void OnDraw() {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("ContentsFinder");
        if (addon == null) {
            addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("RaidFinder");
        }

        if (addon == null || !this.iconsReady || !this.Enabled) {
            return;
        }

        var root = addon->RootNode;
        var header = addon->UldManager.SearchNodeById(4);
        var buttonsHeader = addon->UldManager.SearchNodeById(6);
        var firstButton = addon->UldManager.SearchNodeById(7);
        var languageHeader = addon->UldManager.SearchNodeById(15);
        var japaneseLetter = addon->UldManager.SearchNodeById(17);
        if (root == null || header == null || buttonsHeader == null || firstButton == null || languageHeader == null || japaneseLetter == null) {
            return;
        }

        buttonsHeader->ToggleVisibility(false); // hide the game buttons

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));

        try {
            var windowScale = root->ScaleX;
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(new Vector2(root->X + ((header->X + buttonsHeader->X) * windowScale), root->Y + (buttonsHeader->Y * windowScale)), ImGuiCond.Always);
            if (ImGui.Begin(
                    "ImprovedDutyFinderSettings",
                    ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoBackground
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.AlwaysAutoResize)) {
                var iconSize = firstButton->Width * windowScale;
                var nextButton = firstButton;
                const int nbButtons = 8;
                for (var i = 0; i < nbButtons && nextButton != null; i++) {
                    var setting = this.dutyFinderSettingOrder[i];

                    ImGui.SameLine(nextButton->X * windowScale);
                    var lootRule = (LootRule)GetCurrentSettingValue(DutyFinderSetting.LootRule);
                    var icon = this.GetIcon(setting, lootRule);
                    if (icon != null) {
                        if (ImGui.Selectable($"##DutyFinderSettingButtons{i}", false, ImGuiSelectableFlags.None, new Vector2(iconSize, (header->Height - 5) * windowScale))) {
                            ToggleSetting(setting);
                        }

                        ImGui.SameLine();
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (header->Y * windowScale));
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - iconSize);
                        var tint = GetCurrentSettingValue(setting) == 0 ? new Vector4(0.5f) : new Vector4(1.0f);
                        if (setting == DutyFinderSetting.LevelSync && GetCurrentSettingValue(DutyFinderSetting.UnrestrictedParty) == 0) {
                            tint = new Vector4(0.3f);
                        }

                        ImGui.Image(icon.ImGuiHandle, new Vector2(iconSize), new Vector2(0), new Vector2(1), tint);

                        if (ImGui.IsItemHovered()) {
                            var tooltip = GetTooltip(setting, lootRule);
                            if (setting == DutyFinderSetting.LevelSync) {
                                tooltip += $"\n\nThis setting is only applicable when \"{GetTooltip(DutyFinderSetting.UnrestrictedParty)}\" is enabled.";
                            }

                            ImGui.SetTooltip(tooltip);
                        }
                    } else {
                        ImGui.Text("(?)");
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Failed to load icons." +
                                             "\nThis can happen if your game was corrupted by TexTools. Use the repair function in the launcher to fix this." +
                                             "\nIf it still does not work after a repair, please report this issue.");
                        }
                    }

                    nextButton = nextButton->NextSiblingNode;
                }

                var nextLetter = japaneseLetter;
                const int nbLanguages = 4;
                for (var i = nbButtons; i < nbButtons + nbLanguages; i++) {
                    var setting = this.dutyFinderSettingOrder[i];
                    ImGui.SameLine((languageHeader->X + nextLetter->X - buttonsHeader->X) * windowScale);
                    if (ImGui.Selectable($"##DutyFinderSettingLanguages{i}", false, ImGuiSelectableFlags.None, new Vector2((nextLetter->Width - 2) * windowScale, (header->Height - 5) * windowScale))) {
                        ToggleSetting(setting);
                    }

                    nextLetter = nextLetter->NextSiblingNode;
                }

                ImGui.End();
            }
        }
        finally {
            ImGui.PopStyleVar(2);
        }
    }

    private void ToggleSetting(DutyFinderSetting setting) {
        // always need at least one language enabled
        if (setting is DutyFinderSetting.Ja or DutyFinderSetting.En or DutyFinderSetting.De or DutyFinderSetting.Fr) {
            var nbEnabledLanguages = GetCurrentSettingValue(DutyFinderSetting.Ja)
                                        + GetCurrentSettingValue(DutyFinderSetting.En)
                                        + GetCurrentSettingValue(DutyFinderSetting.De)
                                        + GetCurrentSettingValue(DutyFinderSetting.Fr);
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
            SimpleLog.Error("Tweak appears to be broken, disabling it.");
            Disable();
            return;
        }

        fixed (byte* arrayPtr = array) {
            setContentsFinderSettings(arrayPtr, (nint)Framework.Instance()->GetUiModule());
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
            if ((index % nbSettings != (int)DutyFinderSetting.LootRule && array[index] != 0 && array[index] != 1)
                    || (array[index] != 0 && array[index] != 1 && array[index] != 2)) {
                isArrayValid = false;
                SimpleLog.Error($"Invalid setting value ({array[index]}) for: {(DutyFinderSetting)(index % nbSettings)}");
            }
        }

        // duty server would reject any request without language set
        if (array[(int)DutyFinderSetting.Ja] == 0
                && array[(int)DutyFinderSetting.En] == 0
                && array[(int)DutyFinderSetting.De] == 0
                && array[(int)DutyFinderSetting.Fr] == 0) {
            isArrayValid = false;
            SimpleLog.Error("No language selected, this is impossible.");
        }

        return isArrayValid;
    }
}
