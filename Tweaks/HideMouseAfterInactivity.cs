using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

[TweakCategory(TweakCategory.Command)]
[TweakName("Hide Mouse Cursor After Inactivity")]
[TweakDescription("Hides the mouse cursor after a period of inactivity like video players do.")]
[TweakAutoConfig]
[Changelog("1.8.3.0", "Fixed tweak not working in french.", Author = "Anna")]
public unsafe class HideMouseAfterInactivity : Tweak {

    public class Config : TweakConfig {
        [TweakConfigOption("Hide after (seconds)")]
        public float InactiveSeconds = 0.1f;
        [TweakConfigOption("Don't hide in cutscenes")]
        public bool NoHideInCutscenes;
        [TweakConfigOption("Don't hide in combat")]
        public bool NoHideInCombat = true;
        [TweakConfigOption("Don't hide in instances")]
        public bool NoHideInInstance = true;
        [TweakConfigOption("Don't hide while on interactable")]
        public bool NoHideWhileHovering = true;
    }

    public Config TweakConfig { get; private set; }

    private static class Signatures {
        internal const string MouseButtonHoldState = "8B 05 ?? ?? ?? ?? 48 89 5C 24 ?? 41 8B DF 38 1D";
    }

    private IntPtr mouseButtonHoldState = IntPtr.Zero;
    private Vector2 lastPosition = Vector2.Zero;
    private readonly Stopwatch lastMoved = new();

    protected override void Enable() {
        this.TweakConfig = LoadConfig<Config>() ?? new Config();

        if (mouseButtonHoldState == IntPtr.Zero) {
            Service.SigScanner.TryGetStaticAddressFromSig(Signatures.MouseButtonHoldState, out mouseButtonHoldState);
        }

        Common.FrameworkUpdate += HideMouse;

        base.Enable();
    }

    protected override void Disable() {
        Common.FrameworkUpdate -= HideMouse;
        SaveConfig(TweakConfig);

        base.Disable();
    }

    private void GetInfo() {
        var mouseDown = mouseButtonHoldState != IntPtr.Zero && *(byte*) mouseButtonHoldState > 0;
        if (ImGui.GetMousePos() != lastPosition || mouseDown) {
            this.lastMoved.Restart();
        }

        this.lastPosition = ImGui.GetMousePos();
    }

    private void HideMouse() {
        GetInfo();
        if (TweakConfig.NoHideInCutscenes && Service.Condition.Cutscene()) return;
        if (TweakConfig.NoHideInCombat && Service.Condition[ConditionFlag.InCombat]) return;
        if (TweakConfig.NoHideInInstance && Service.Condition.Duty()) return;
        var stage = AtkStage.Instance();
        if (TweakConfig.NoHideWhileHovering && stage->AtkCursor.Type != AtkCursor.CursorType.Arrow && !Service.Condition.Cutscene()) return;

        if (lastMoved.Elapsed > TimeSpan.FromSeconds(TweakConfig.InactiveSeconds)) {
            stage->AtkCursor.Hide();
        }
    }
}