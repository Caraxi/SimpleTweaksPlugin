using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class ParameterBarAdjustments : UiAdjustments.SubTweak {
    public override string Name => "Parameter Bar Adjustments";
    public override string Description => "Allows hiding or moving specific parts of the parameter bar (HP and mana bars).";
    protected override string Author => "Aireil";
    public override IEnumerable<string> Tags => new[] {"parameter", "hp", "mana", "bar"};

    public class Configs : TweakConfig {
        public HideAndOffsetConfig TargetCycling = new() { OffsetX = 100, OffsetY = 1 };

        public bool HideHpTitle;
        public HideAndOffsetConfig HpBar = new() { OffsetX = 96, OffsetY = 12 };
        public HideAndOffsetConfig HpValue = new() { OffsetX = 24, OffsetY = 7 };

        public bool HideMpTitle;
        public HideAndOffsetConfig MpBar = new() { OffsetX = 256, OffsetY = 12 };
        public HideAndOffsetConfig MpValue = new() { OffsetX = 24, OffsetY = 7 };

        public bool AutoHideMp;

        public Vector4 HpColor = new Vector4(20 / ColorMultiplier, 75 / ColorMultiplier, 0 / ColorMultiplier, 1);
        public Vector4 MpColor = new Vector4(120 / ColorMultiplier, 0 / ColorMultiplier, 60 / ColorMultiplier, 1);
        public Vector4 GpColor = new Vector4(0 / ColorMultiplier, 70 / ColorMultiplier, 100 / ColorMultiplier, 1);
        public Vector4 CpColor = new Vector4(70 / ColorMultiplier, 10 / ColorMultiplier, 100 / ColorMultiplier, 1);
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

    public override void Enable() {
        var nbJobs = Service.Data.Excel.GetSheet<ClassJob>()?.ColumnCount ?? 0;
        var classJobCategoriesSheet = Service.Data.Excel.GetSheet<ClassJobCategory>();
        var doLParser = classJobCategoriesSheet?.GetRowParser(32);
        var doHParser = classJobCategoriesSheet?.GetRowParser(33);

        for (var job = 0; job < nbJobs; job++) {
            if (doLParser?.ReadColumn<bool>(job + 1) ?? false) {
                this.doLIds.Add((uint) job);
            }
            if (doHParser?.ReadColumn<bool>(job + 1) ?? false) {
                this.doHIds.Add((uint) job);
            }
        }

        Config = LoadConfig<Configs>() ?? new Configs();
        Service.Framework.Update += OnFrameworkUpdate;
        base.Enable();
    }

    public override void Disable() {
        Service.Framework.Update -= OnFrameworkUpdate;
        UpdateParameterBar(true);
        SaveConfig(Config);
        base.Disable();
    }

    private void OnFrameworkUpdate(Framework framework) {
        try {
            UpdateParameterBar();
        }
        catch (Exception ex) {
            SimpleLog.Error(ex);
        }
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
            if (ImGui.Button($"{(char) FontAwesomeIcon.CircleNotch}##resetOffset_{label}")) {
                config.OffsetX = defConfig.OffsetX;
                config.OffsetY = defConfig.OffsetY;
                hasChanged = true;
            }
            ImGui.PopFont();
        }

        return hasChanged;
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
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

        hasChanged |= ImGui.ColorEdit4(LocString("HP Bar Color"), ref Config.HpColor);
        hasChanged |= ImGui.ColorEdit4(LocString("MP Bar Color"), ref Config.MpColor);
        hasChanged |= ImGui.ColorEdit4(LocString("GP Bar Color"), ref Config.GpColor);
        hasChanged |= ImGui.ColorEdit4(LocString("CP Bar Color"), ref Config.CpColor);

        if (hasChanged) UpdateParameterBar(true);
    };

    private const byte Byte00 = 0x00;
    private const byte ByteFF = 0xFF;

    private const float ColorMultiplier = 120f;

    private readonly uint[] autoHideMpClassJobs = {
        1, 2, 3, 4, 5, 20, 21, 22, 23, 29, 30, 31, 34, 37, 38, 39
    };

    private void UpdateParameter(AtkComponentNode* node, HideAndOffsetConfig barConfig, HideAndOffsetConfig valueConfig, Vector4 barColor, bool hideTitle, bool hideMp = false) {
        var valueNode = node->Component->UldManager.SearchNodeById(3);
        var titleNode = node->Component->UldManager.SearchNodeById(2);
        var textureNode = node->Component->UldManager.SearchNodeById(8);
        var textureNode2 = node->Component->UldManager.SearchNodeById(4);
        var gridNode = node->Component->UldManager.SearchNodeById(7);
        var grindNode2 = node->Component->UldManager.SearchNodeById(6);
        var gridNode3= node->Component->UldManager.SearchNodeById(5);

        node->AtkResNode.SetPositionFloat(barConfig.OffsetX, barConfig.OffsetY);
        valueNode->SetPositionFloat(valueConfig.OffsetX, valueConfig.OffsetY);

        valueNode->Color.A = hideMp || valueConfig.Hide ? Byte00 : ByteFF;
        titleNode->Color.A = hideMp || hideTitle ? Byte00 : ByteFF;
        gridNode->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        grindNode2->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        gridNode3->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        textureNode->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;
        textureNode2->Color.A = hideMp || barConfig.Hide ? Byte00 : ByteFF;

        gridNode3->AddRed = (ushort)(barColor.X * ColorMultiplier);
        gridNode3->AddGreen = (ushort)(barColor.Y * ColorMultiplier);
        gridNode3->AddBlue = (ushort)(barColor.Z * ColorMultiplier);
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
        Vector4 mpColor;
        var classJobId = Service.ClientState?.LocalPlayer?.ClassJob.Id;
        if (classJobId != null && this.doLIds.Contains(classJobId.Value)) {
            mpColor = reset ? DefaultConfig.GpColor : Config.GpColor;
        } else if (classJobId != null && this.doHIds.Contains(classJobId.Value)) {
            mpColor = reset ? DefaultConfig.CpColor : Config.CpColor;
        } else {
            mpColor = reset ? DefaultConfig.MpColor : Config.MpColor;
        }

        var hideMp = !reset && Config.AutoHideMp && Service.Condition[ConditionFlag.RolePlaying] == false && classJobId != null && autoHideMpClassJobs.Contains(classJobId.Value);
        var mpNode = (AtkComponentNode*) parameterWidgetUnitBase->UldManager.SearchNodeById(4);
        if (mpNode != null) UpdateParameter(mpNode, reset ? DefaultConfig.MpBar : Config.MpBar, reset ? DefaultConfig.MpValue : Config.MpValue, mpColor, reset ? DefaultConfig.HideHpTitle : Config.HideMpTitle, hideMp);

        // HP
        var hpNode = (AtkComponentNode*) parameterWidgetUnitBase->UldManager.SearchNodeById(3);
        if (hpNode != null) UpdateParameter(hpNode, reset ? DefaultConfig.HpBar : Config.HpBar, reset ? DefaultConfig.HpValue : Config.HpValue, reset ? DefaultConfig.HpColor : Config.HpColor, reset ? DefaultConfig.HideHpTitle : Config.HideHpTitle);
    }
}