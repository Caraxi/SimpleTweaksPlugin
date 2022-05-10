using System;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class AlwaysYes : UiAdjustments.SubTweak {
    public override string Name => "Always Yes";
    public override string Description => "Default cursor to yes when using confirm (num 0).";
    protected override string Author => "Aireil";

    private HookWrapper<Common.AddonOnSetup> selectYesnoOnSetupHook;
    private HookWrapper<Common.AddonOnSetup> contentsFinderConfirmOnSetupHook;
    private HookWrapper<Common.AddonOnSetup> shopCardDialogOnSetupHook;
    private HookWrapper<Common.AddonOnSetup> retainerTaskAskOnSetupHook;
    private HookWrapper<Common.AddonOnSetup> retainerTaskResultOnSetupHook;

    public class Configs : TweakConfig {
        public bool YesNo = true;
        public bool DutyConfirmation = true;
        public bool CardsShop = true;
        public bool RetainerVentures = true;
    }

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox("Enable for most yes/(hold)/no dialogs", ref Config.YesNo);
        hasChanged |= ImGui.Checkbox("Enable for duty confirmations", ref Config.DutyConfirmation);
        hasChanged |= ImGui.Checkbox("Enable for TT cards selling", ref Config.CardsShop);
        hasChanged |= ImGui.Checkbox("Enable for retainer ventures", ref Config.RetainerVentures);

        if (hasChanged) {
            SaveConfig(Config);
        }
    };

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();

        selectYesnoOnSetupHook ??= Common.Hook<Common.AddonOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 40 44 8B F2 0F 29 74 24", SelectYesnoOnSetupDetour);
        selectYesnoOnSetupHook?.Enable();

        contentsFinderConfirmOnSetupHook ??= Common.Hook<Common.AddonOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 30 44 8B F2 49 8B E8 BA ?? ?? ?? ?? 48 8B D9", ContentsFinderConfirmOnSetupDetour);
        contentsFinderConfirmOnSetupHook?.Enable();

        shopCardDialogOnSetupHook ??= Common.Hook<Common.AddonOnSetup>("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 54 41 56 41 57 48 83 EC 50 48 8B F9", ShopCardDialogOnSetupDetour);
        shopCardDialogOnSetupHook?.Enable();

        retainerTaskAskOnSetupHook ??= Common.Hook<Common.AddonOnSetup>("40 53 48 83 EC 30 48 8B D9 83 FA 03", RetainerTaskAskOnSetupDetour);
        retainerTaskAskOnSetupHook?.Enable();

        retainerTaskResultOnSetupHook ??= Common.Hook<Common.AddonOnSetup>("48 89 5C 24 ?? 55 56 57 48 83 EC 40 8B F2", RetainerTaskResultOnSetupDetour);
        retainerTaskResultOnSetupHook?.Enable();

        base.Enable();
    }

    private void* SelectYesnoOnSetupDetour(AtkUnitBase* unitBase, void* a2, void* a3) {
        var retVal = selectYesnoOnSetupHook.Original(unitBase, a2, a3);

        try {
            if (Config.YesNo) {
                SetFocusYes(unitBase, 8, 9);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Exception in SelectYesnoOnSetupDetour");
        }

        return retVal;
    }

    private void* ContentsFinderConfirmOnSetupDetour(AtkUnitBase* unitBase, void* a2, void* a3) {
        var retVal = contentsFinderConfirmOnSetupHook.Original(unitBase, a2, a3);

        try {
            if (Config.DutyConfirmation) {
                SetFocusYes(unitBase, 63);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Exception in ContentsFinderConfirmOnSetupDetour");
        }

        return retVal;
    }

    private void* ShopCardDialogOnSetupDetour(AtkUnitBase* unitBase, void* a2, void* a3) {
        var retVal = shopCardDialogOnSetupHook.Original(unitBase, a2, a3);

        try {
            if (Config.CardsShop) {
                SetFocusYes(unitBase, 16);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Exception in ShopCardDialogOnSetupDetour");
        }

        return retVal;
    }

    private void* RetainerTaskAskOnSetupDetour(AtkUnitBase* unitBase, void* a2, void* a3) {
        var retVal = this.retainerTaskAskOnSetupHook.Original(unitBase, a2, a3);

        try {
            if (Config.RetainerVentures) {
                SetFocusYes(unitBase, 40);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Exception in RetainerTaskAskOnSetupDetour");
        }

        return retVal;
    }

    private void* RetainerTaskResultOnSetupDetour(AtkUnitBase* unitBase, void* a2, void* a3) {
        var retVal = this.retainerTaskResultOnSetupHook.Original(unitBase, a2, a3);

        try {
            if (Config.RetainerVentures) {
                SetFocusYes(unitBase, 20);
            }
        } catch (Exception ex) {
            PluginLog.Error(ex, "Exception in RetainerTaskResultOnSetupDetour");
        }

        return retVal;
    }

    private static void SetFocusYes(AtkUnitBase* unitBase, uint yesButtonId, uint? yesHoldButtonId = null) {
        var yesButton = unitBase->UldManager.SearchNodeById(yesButtonId);
        if (yesButton == null) return;

        var isYesHoldVersion = yesHoldButtonId != null && !yesButton->IsVisible;
        if (isYesHoldVersion) {
            yesButton = unitBase->UldManager.SearchNodeById(yesHoldButtonId.Value);
            if (yesButton == null) return;
        }

        var yesCollision = ((AtkComponentNode *)yesButton)->Component->UldManager.SearchNodeById(isYesHoldVersion ? 7u : 4u);
        if (yesCollision == null) return;

        unitBase->SetFocusNode(yesCollision);
        unitBase->CursorTarget = yesCollision;
    }

    public override void Disable() {
        selectYesnoOnSetupHook?.Disable();
        contentsFinderConfirmOnSetupHook?.Disable();
        shopCardDialogOnSetupHook?.Disable();
        retainerTaskAskOnSetupHook?.Disable();
        retainerTaskResultOnSetupHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        selectYesnoOnSetupHook?.Dispose();
        contentsFinderConfirmOnSetupHook?.Dispose();
        shopCardDialogOnSetupHook?.Dispose();
        retainerTaskAskOnSetupHook?.Dispose();
        retainerTaskResultOnSetupHook?.Disable();
        base.Dispose();
    }
}
