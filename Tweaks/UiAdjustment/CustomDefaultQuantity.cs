using System;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Customize default deposit and withdraw quantity")]
[TweakDescription("Allows setting a custom amount to deposit or withdraw when using the 'Retrieve Quantity' and 'Entrust Quantity' options.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.4.0")]
[TweakCategory(TweakCategory.UI)]
public unsafe class CustomDefaultQuantity : Tweak {
    public class ModeConfig {
        public bool Enable;
        public bool UsePercentage;
        public int Quantity = 1;
        public int Percentage = 50;
    }

    public class Configs : TweakConfig {
        public ModeConfig Withdraw = new();
        public ModeConfig Deposit = new();
    }

    [TweakConfig] public Configs Config { get; private set; }

    protected void DrawConfig() {
        void DrawModeConfig(ModeConfig modeConfig, string label) {
            ImGui.Checkbox($"Apply to '{label}'", ref modeConfig.Enable);
            if (modeConfig.Enable) {
                using (ImRaii.PushIndent()) {
                    ImGui.Checkbox($"Use Percentage##{label}", ref modeConfig.UsePercentage);
                    ImGui.SetNextItemWidth(200);
                    if (modeConfig.UsePercentage) {
                        if (ImGui.SliderInt($"Percent of Max##{label}", ref modeConfig.Percentage, 0, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp)) {
                            if (modeConfig.Percentage < 0) modeConfig.Percentage = 0;
                            if (modeConfig.Percentage > 100) modeConfig.Percentage = 100;
                        }
                    } else {
                        if (ImGui.InputInt($"Default Quantity##{label}", ref modeConfig.Quantity)) {
                            if (modeConfig.Quantity < 1) modeConfig.Quantity = 1;
                        }
                    }
                }
            }
        }

        DrawModeConfig(Config.Withdraw, "Retrieve Quantity");
        DrawModeConfig(Config.Deposit, "Entrust Quantity");
    }

    private readonly string textWithdraw = Service.Data.GetExcelSheet<Addon>().GetRow(914).Text.ExtractText();
    private readonly string textDeposit = Service.Data.GetExcelSheet<Addon>().GetRow(915).Text.ExtractText();

    [AddonPreSetup("InputNumeric")]
    public void AddonSetup(AtkUnitBase* atkUnitBase) {
        if (atkUnitBase == null) return;
        if (atkUnitBase->AtkValuesCount < 7) return;

        var textValue = atkUnitBase->AtkValues + 6;
        if (textValue->Type != ValueType.String) return;
        var text = Common.ReadSeString(textValue->String)?.TextValue;
        if (string.IsNullOrEmpty(text)) return;

        var mode = text.Equals(textWithdraw) ? Config.Withdraw : text.Equals(textDeposit) ? Config.Deposit : null;
        if (mode is not { Enable: true }) return;

        var minValue = atkUnitBase->AtkValues + 2;
        var maxValue = atkUnitBase->AtkValues + 3;
        var defaultValue = atkUnitBase->AtkValues + 4;

        if (minValue->Type != ValueType.UInt || maxValue->Type != ValueType.UInt || defaultValue->Type != ValueType.UInt) return;

        var min = minValue->UInt;
        var max = maxValue->UInt;

        var value = Math.Clamp(mode.UsePercentage ? (uint)Math.Ceiling(max * (mode.Percentage / 100f)) : (uint)mode.Quantity, min, max);
        defaultValue->UInt = value;
    }
}
