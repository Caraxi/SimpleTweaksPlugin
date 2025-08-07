using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.Events;
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
        public float InactiveSeconds = 3f;

        [TweakConfigOption("Don't hide in cutscenes")]
        public bool NoHideInCutscenes;

        [TweakConfigOption("Don't hide in combat")]
        public bool NoHideInCombat = true;

        [TweakConfigOption("Don't hide in instances")]
        public bool NoHideInInstance = true;

        [TweakConfigOption("Don't hide while on interactable")]
        public bool NoHideWhileHovering = true;
    }

    protected void DrawConfig() {
        ImGui.InputFloat("Hide after (seconds)", ref TweakConfig.InactiveSeconds, 0.1f);
    }

    public Config TweakConfig { get; private set; }

    private Vector2 lastPosition = Vector2.Zero;
    private readonly Stopwatch lastMoved = new();

    private void GetInfo() {
        var mouseDown = *InputManager.GetMouseButtonHoldState() > 0;
        if (ImGui.GetMousePos() != lastPosition || mouseDown) lastMoved.Restart();
        lastPosition = ImGui.GetMousePos();
    }

    [FrameworkUpdate]
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
