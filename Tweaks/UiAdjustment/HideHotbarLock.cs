using System;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[Changelog("1.8.9.2", "Added an option to change which keys show the lock")]
[TweakCategory(TweakCategory.UI)]
[TweakName("Hide Hotbar Lock")]
[TweakDescription("Hides the hotbar lock button, with an option to make it visible while holding a modifier combo.")]
public unsafe class HideHotbarLock : Tweak {
    public class Configs : TweakConfig {
        public bool ShowWhileHoldingShift;
        public bool Shift = true;
        public bool Ctrl;
        public bool Alt;
    }

    public Configs Config { get; private set; }

    private Vector2 boxSize = Vector2.Zero;

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.BeginGroup();
        var s2 = ImGui.CalcTextSize("Show while holding");
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        ImGui.Dummy(new Vector2(boxSize.Y / 2 - s2.Y / 2));
        ImGui.Checkbox("Show while holding ", ref Config.ShowWhileHoldingShift);
        ImGui.PopStyleVar();
        ImGui.Dummy(Vector2.Zero);
        ImGui.EndGroup();
        ImGui.SameLine();
        ImGui.BeginGroup();
        hasChanged |= ImGui.Checkbox("Shift", ref Config.Shift);
        hasChanged |= ImGui.Checkbox("Ctrl", ref Config.Ctrl);
        hasChanged |= ImGui.Checkbox("Alt", ref Config.Alt);
        ImGui.EndGroup();
        boxSize = ImGui.GetItemRectSize();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(min - ImGui.GetStyle().ItemSpacing, max + ImGui.GetStyle().ItemSpacing, 0x99999999);
    }

    protected override void ConfigChanged() {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        if (Config.ShowWhileHoldingShift) Common.FrameworkUpdate += OnFrameworkUpdate;
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        SetLockVisible();
        ConfigChanged();
    }

    [AddonPostRequestedUpdate("_ActionBar")]
    private void AfterAddonUpdate(AtkUnitBase* atkUnitBase) {
        try {
            var lockNode = atkUnitBase->GetNodeById(21)->GetAsAtkComponentNode();
            if (lockNode == null) return;
            lockNode->AtkResNode.ToggleVisibility(LockVisible);
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    [FrameworkUpdate] private void OnFrameworkUpdate() => SetLockVisible();

    private bool LockVisible => Config.ShowWhileHoldingShift && (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt);

    private void SetLockVisible(bool? visible = null) {
        var unitBase = Common.GetUnitBase("_ActionBar");
        if (unitBase == null) return;
        var lockNode = unitBase->GetNodeById(21);
        if (lockNode == null) return;
        var lockComponentNode = lockNode->GetAsAtkComponentNode();
        if (lockComponentNode == null) return;
        lockComponentNode->AtkResNode.ToggleVisibility(visible ?? LockVisible);
    }

    protected override void Disable() {
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        SetLockVisible(true);
        SaveConfig(Config);
    }
}
