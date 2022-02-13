using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public unsafe class HideMouseAfterInactivity : Tweak {
    public override string Name => "Hide Mouse Cursor After Inactivity";
    public override string Description => "Hides the mouse cursor after a period of inactivity like video players do.";
    protected override string Author => "Anna";

    public class Config : TweakConfig {
        public float InactiveSeconds = 3.0f;
        public bool NoHideInCutscenes;
        public bool NoHideInCombat = true;
        public bool NoHideInInstance = true;
        public bool NoHideWhileHovering = true;
    }

    public Config TweakConfig { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool change) => {
        change |= ImGui.InputFloat("Hide after (seconds)", ref TweakConfig.InactiveSeconds, 0.1f);
        change |= ImGui.Checkbox("Don't hide in cutscenes", ref TweakConfig.NoHideInCutscenes);
        change |= ImGui.Checkbox("Don't hide in combat", ref TweakConfig.NoHideInCombat);
        change |= ImGui.Checkbox("Don't hide in instances", ref TweakConfig.NoHideInInstance);
        change |= ImGui.Checkbox("Don't hide while on interactable", ref TweakConfig.NoHideWhileHovering);
    };

    private static class Signatures {
        internal const string MouseButtonHoldState = "8B 05 ?? ?? ?? ?? 48 89 5C 24 ?? 41 8B DF 38 1D";
    }

    private IntPtr mouseButtonHoldState = IntPtr.Zero;
    private Vector2 lastPosition = Vector2.Zero;
    private readonly Stopwatch lastMoved = new();

    public override void Enable() {
        this.TweakConfig = LoadConfig<Config>() ?? new Config();

        if (mouseButtonHoldState == IntPtr.Zero) {
            Service.SigScanner.TryGetStaticAddressFromSig(Signatures.MouseButtonHoldState, out mouseButtonHoldState);
        }

        Service.Framework.Update += HideMouse;

        base.Enable();
    }

    public override void Disable() {
        Service.Framework.Update -= HideMouse;
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

    private void HideMouse(Framework framework) {
        GetInfo();
        if (TweakConfig.NoHideInCutscenes && Service.Condition.Cutscene()) return;
        if (TweakConfig.NoHideInCombat && Service.Condition[ConditionFlag.InCombat]) return;
        if (TweakConfig.NoHideInInstance && Service.Condition.Duty()) return;
        var stage = AtkStage.GetSingleton();
        if (TweakConfig.NoHideWhileHovering && stage->AtkCursor.Type != AtkCursor.CursorType.Arrow && !Service.Condition.Cutscene()) return;

        if (lastMoved.Elapsed > TimeSpan.FromSeconds(TweakConfig.InactiveSeconds)) {
            stage->AtkCursor.Hide();
        }
    }
}