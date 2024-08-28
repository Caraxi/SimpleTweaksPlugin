using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Parameter Bar Adjustments")]
[TweakDescription("Allows hiding or moving specific parts of the parameter bar (HP and mana bars).")]
[TweakAuthor("Aireil")]
[Changelog("1.10.0.1", "Hide MP bar on Viper")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[TweakTags("parameter", "hp", "mana", "bar")]
public unsafe class ParameterBarAdjustments : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public HideAndOffsetConfig TargetCycling = new() { OffsetX = 100, OffsetY = 1 };

        public bool HideHpTitle;
        public HideAndOffsetConfig HpBar = new() { OffsetX = 96, OffsetY = 12 };
        public HideAndOffsetConfig HpValue = new() { OffsetX = 24, OffsetY = 7 };

        public bool HideMpTitle;
        public HideAndOffsetConfig MpBar = new() { OffsetX = 256, OffsetY = 12 };
        public HideAndOffsetConfig MpValue = new() { OffsetX = 24, OffsetY = 7 };

        public bool AutoHideMp;
        public bool CenterHpWithMpHidden;

        // deprecated versions
        public Vector4 HpColor;
        public Vector4 MpColor;
        public Vector4 GpColor;
        public Vector4 CpColor;

        // updated versions
        public Vector3 HpAddRGB = new(20 ,75 ,0);
        public Vector3 MpAddRGB = new(120, 0, 60);
        public Vector3 GpAddRGB = new(0, 70, 100);
        public Vector3 CpAddRGB = new(70, 10, 100);

        public void TransferColorConfigs() // take deprecated configs if found, and convert them into the new format
        {
            void TransferConfig(ref Vector4 color, ref Vector3 add)
            {
                if (color == default) return;
                add = new Vector3(color.X, color.Y, color.Z) * 120f;
                color = default;
            }

            TransferConfig(ref HpColor, ref HpAddRGB);
            TransferConfig(ref MpColor, ref MpAddRGB);
            TransferConfig(ref GpColor, ref GpAddRGB);
            TransferConfig(ref CpColor, ref CpAddRGB);
        }
    }

    public const float AddRange = 180f; // the +/- range of AddRGB values when using a color picker

    private static Vector3 ConvertRGBToAdd(Vector3 rgb)
    {
        return (rgb * 2 * AddRange) - new Vector3(AddRange);
    }

    private static Vector3 ConvertAddToRGB(Vector3 add)
    {
        return (add + new Vector3(AddRange)) / (2 * AddRange);
    }

    private static bool AddRGBPicker(string label, ref Vector3 addVector)
    {
        var rgbVector = ConvertAddToRGB(addVector);

        if (ImGui.ColorEdit3(label, ref rgbVector))
        {
            addVector = ConvertRGBToAdd(rgbVector);
            return true;
        }

        return false;
    }

    public class HideAndOffsetConfig {
        public bool Hide;
        public int OffsetX;
        public int OffsetY;
    }

    public Configs Config { get; private set; }

    private static readonly Configs DefaultConfig = new();

    private List<uint> doLIds = new();
    private List<uint> doHIds = new();

    private bool inPVP = false;

    protected override void Setup() {
        AddChangelog("1.8.1.2", "Fixed positioning of HP bar.");
        AddChangelog("1.8.1.1", "Added option to center HP bar when MP bar is hidden.");
    }

    protected override void Enable() {
        var nbJobs = Service.Data.Excel.GetSheet<ClassJob>()?.ColumnCount ?? 0;
        var classJobCategoriesSheet = Service.Data.Excel.GetSheet<ClassJobCategory>();
        var doLParser = classJobCategoriesSheet?.GetRowParser(32);
        var doHParser = classJobCategoriesSheet?.GetRowParser(33);

        for (var job = 0; job < nbJobs; job++) {
            if (doLParser?.ReadColumn<bool>(job + 1) ?? false) {
                this.doLIds.Add((uint)job);
            }

            if (doHParser?.ReadColumn<bool>(job + 1) ?? false) {
                this.doHIds.Add((uint)job);
            }
        }

        Config = LoadConfig<Configs>() ?? new Configs();
        Config.TransferColorConfigs();

        OnTerritoryChanged(Service.ClientState.TerritoryType);
        base.Enable();
    }


    protected override void Disable() {
        UpdateParameterBar(true);
        SaveConfig(Config);
        base.Disable();
    }

    [FrameworkUpdate]
    private void OnFrameworkUpdate() {
        try {
            UpdateParameterBar();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    [TerritoryChanged]
    private void OnTerritoryChanged(ushort territoryType) {
        var territory = Service.Data.Excel.GetSheet<TerritoryType>()?.GetRow(territoryType);
        if (territory == null) return;
        inPVP = territory.IsPvpZone;
    }

    private bool VisibilityAndOffsetEditor(string label, ref HideAndOffsetConfig config, HideAndOffsetConfig defConfig) {
        var hasChanged = false;
        var positionOffset = 185 * ImGui.GetIO().FontGlobalScale;
        var resetOffset = 250 * ImGui.GetIO().FontGlobalScale;

        hasChanged |= ImGui.Checkbox(label, ref config.Hide);
        if (!config.Hide) {
            ImGui.SameLine();
            ImGui.SetCursorPosX(positionOffset);
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt($"##offsetX_{label}", ref config.OffsetX);
            ImGui.SameLine();
            ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale));
            ImGui.SetNextItemWidth(100 * ImGui.GetIO().FontGlobalScale);
            hasChanged |= ImGui.InputInt($"Offset##offsetY_{label}", ref config.OffsetY);
            ImGui.SameLine();
            ImGui.SetCursorPosX(positionOffset + (105 * ImGui.GetIO().FontGlobalScale) + resetOffset);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char)FontAwesomeIcon.CircleNotch}##resetOffset_{label}")) {
                config.OffsetX = defConfig.OffsetX;
                config.OffsetY = defConfig.OffsetY;
                hasChanged = true;
            }

            ImGui.PopFont();
        }

        return hasChanged;
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= VisibilityAndOffsetEditor(LocString("Hide Target Cycling"), ref Config.TargetCycling, DefaultConfig.TargetCycling);
        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);

        hasChanged |= VisibilityAndOffsetEditor(LocString("Hide HP Bar"), ref Config.HpBar, DefaultConfig.HpBar);
        hasChanged |= ImGui.Checkbox(LocString("Hide 'HP' Text"), ref Config.HideHpTitle);
        hasChanged |= VisibilityAndOffsetEditor(LocString("Hide HP Value"), ref Config.HpValue, DefaultConfig.HpValue);
        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);

        hasChanged |= VisibilityAndOffsetEditor(LocString("Hide MP Bar"), ref Config.MpBar, DefaultConfig.MpBar);
        hasChanged |= ImGui.Checkbox(LocString("Hide 'MP' Text"), ref Config.HideMpTitle);
        hasChanged |= VisibilityAndOffsetEditor(LocString("Hide MP Value"), ref Config.MpValue, DefaultConfig.MpValue);

        hasChanged |= ImGui.Checkbox(LocString("AutoHideMp", "Hide MP Bar on jobs that don't use MP"), ref Config.AutoHideMp);
        if (Config.AutoHideMp)
            hasChanged |= ImGui.Checkbox(LocString("CenterHpWithMpHidden", "Center the HP Bar on jobs that don't use MP"), ref Config.CenterHpWithMpHidden);


        hasChanged |= AddRGBPicker(LocString("HP Bar Color"), ref Config.HpAddRGB);
        hasChanged |= AddRGBPicker(LocString("MP Bar Color"), ref Config.MpAddRGB);
        hasChanged |= AddRGBPicker(LocString("GP Bar Color"), ref Config.GpAddRGB);
        hasChanged |= AddRGBPicker(LocString("CP Bar Color"), ref Config.CpAddRGB);

        if (hasChanged) UpdateParameterBar(true);
    }

    private const byte Byte00 = 0x00;
    private const byte ByteFF = 0xFF;

    private readonly uint[] autoHideMpClassJobs = { 1, 2, 3, 4, 5, 20, 21, 22, 23, 29, 30, 31, 34, 37, 38, 39, 41 };

    private void UpdateParameter(AtkComponentNode* node, HideAndOffsetConfig barConfig, HideAndOffsetConfig valueConfig,
        Vector3 barAdd, Vector3 barMultiply, bool hideTitle, bool hideMp = false) {
        var valueNode = node->Component->UldManager.SearchNodeById(3);
        var titleNode = node->Component->UldManager.SearchNodeById(2);
        var textureNode = node->Component->UldManager.SearchNodeById(8);
        var textureNode2 = node->Component->UldManager.SearchNodeById(4);
        var gridNode = node->Component->UldManager.SearchNodeById(7);
        var gridNode2 = node->Component->UldManager.SearchNodeById(6);
        var gridNode3 = node->Component->UldManager.SearchNodeById(5);

        node->AtkResNode.SetPositionFloat(barConfig.OffsetX, barConfig.OffsetY);
        valueNode->SetPositionFloat(valueConfig.OffsetX, valueConfig.OffsetY);

        valueNode->Color.A = hideMp || valueConfig.Hide ? Byte00 : ByteFF;
        titleNode->Color.A = hideMp || hideTitle ? Byte00 : ByteFF;
        gridNode->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        gridNode2->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        gridNode3->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        textureNode->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        textureNode2->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;

        gridNode3->AddRed = (short)barAdd.X;
        gridNode3->AddGreen = (short)barAdd.Y;
        gridNode3->AddBlue = (short)barAdd.Z;

        gridNode3->MultiplyRed = (byte)barMultiply.X;
        gridNode3->MultiplyGreen = (byte)barMultiply.Y;
        gridNode3->MultiplyBlue = (byte)barMultiply.Z;
    }

    private void UpdateParameterBar(bool reset = false) {
        var parameterWidgetUnitBase = Common.GetUnitBase("_ParameterWidget");
        if (parameterWidgetUnitBase == null) return;

        // Target cycling
        var targetCyclingNode = parameterWidgetUnitBase->UldManager.SearchNodeById(2);
        if (targetCyclingNode != null) {
            targetCyclingNode->SetPositionFloat(reset ? DefaultConfig.TargetCycling.OffsetX : Config.TargetCycling.OffsetX, reset ? DefaultConfig.TargetCycling.OffsetY : Config.TargetCycling.OffsetY);
            targetCyclingNode->Color.A = Config.TargetCycling.Hide && !reset ? Byte00 : ByteFF;
        }

        // MP
        Vector3 mpAdd;
        Vector3 mpMultiply;
        var classJobId = Service.ClientState?.LocalPlayer?.ClassJob.Id;
        if (classJobId != null && doLIds.Contains(classJobId.Value)) {
            mpAdd = reset ? DefaultConfig.GpAddRGB : Config.GpAddRGB;
            mpMultiply = reset ? new(75, 75, 80) : new(75);
        } else if (classJobId != null && doHIds.Contains(classJobId.Value)) {
            mpAdd = reset ? DefaultConfig.CpAddRGB : Config.CpAddRGB;
            mpMultiply = reset ? new(80, 75, 80) : new(75);
        } else {
            mpAdd = reset ? DefaultConfig.MpAddRGB : Config.MpAddRGB;
            mpMultiply = reset ? new(90, 75, 75) : new(75);
        }

        var hideMp = !reset && Config.AutoHideMp && !inPVP && Service.Condition[ConditionFlag.RolePlaying] == false && classJobId != null && autoHideMpClassJobs.Contains(classJobId.Value);
        var mpNode = (AtkComponentNode*)parameterWidgetUnitBase->UldManager.SearchNodeById(4);

        if (mpNode != null) UpdateParameter(mpNode, reset ? DefaultConfig.MpBar : Config.MpBar, reset ? DefaultConfig.MpValue : Config.MpValue, mpAdd, mpMultiply, reset ? DefaultConfig.HideHpTitle : Config.HideMpTitle, hideMp);

        // HP
        var hpNode = (AtkComponentNode*)parameterWidgetUnitBase->UldManager.SearchNodeById(3);
        if (hpNode != null) UpdateParameter(hpNode, reset ? DefaultConfig.HpBar : Config.HpBar, reset ? DefaultConfig.HpValue : Config.HpValue, reset ? DefaultConfig.HpAddRGB : Config.HpAddRGB, reset ? new(80, 80, 40) : new(75), reset ? DefaultConfig.HideHpTitle : Config.HideHpTitle);

        var centerHpBar = hideMp && Config.CenterHpWithMpHidden;
        if (centerHpBar)
            hpNode->AtkResNode.SetPositionFloat(Config.HpBar.OffsetX + ((Config.MpBar.OffsetX - Config.HpBar.OffsetX) / 2), Config.HpBar.OffsetY + ((Config.MpBar.OffsetY - Config.HpBar.OffsetY) / 2));
    }
}
