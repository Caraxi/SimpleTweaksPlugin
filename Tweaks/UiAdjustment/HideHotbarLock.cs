using System;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class HideHotbarLock : Tweak {
    public override string Name => "Hide Hotbar Lock";
    public override string Description => "Hides the hotbar lock button, with an option to make it visible whole holding SHIFT.";

    public class Configs : TweakConfig {
        [TweakConfigOption("Show while holding shift")]
        public bool ShowWhileHoldingShift = false;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        if (Config.ShowWhileHoldingShift) Common.FrameworkUpdate += OnFrameworkUpdate;
        base.ConfigChanged();
    }

    private HookWrapper<Common.AddonOnUpdate> onAddonUpdate;
    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        onAddonUpdate ??= Common.HookAfterAddonUpdate("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B DA 48 8B F1 E8 ?? ?? ?? ?? 48 8B 7B 30", AfterAddonUpdate);
        onAddonUpdate?.Enable();
        SetLockVisible();
        ConfigChanged();
        base.Enable();
    }

    private void AfterAddonUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        try {
            var lockNode = atkUnitBase->GetNodeById(21)->GetAsAtkComponentNode();
            if (lockNode == null) return;
            lockNode->AtkResNode.ToggleVisibility(LockVisible);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    private void OnFrameworkUpdate() {
        SetLockVisible();
    }
    

    private bool LockVisible => Config.ShowWhileHoldingShift && Service.KeyState[VirtualKey.SHIFT];
    
    private void SetLockVisible(bool? visible = null) {
        var unitBase = Common.GetUnitBase("_ActionBar");
        if (unitBase == null) return;
        var lockNode = unitBase->GetNodeById(21);
        if (lockNode == null) return;
        var lockComponentNode = lockNode->GetAsAtkComponentNode();
        if (lockComponentNode == null) return;
        lockComponentNode->AtkResNode.ToggleVisibility(visible ?? LockVisible);
    }
    

    public override void Disable() {
        onAddonUpdate?.Disable();
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        SetLockVisible(true);
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        onAddonUpdate?.Dispose();
        base.Dispose();
    }
}
