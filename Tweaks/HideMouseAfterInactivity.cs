using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public bool ShouldSerializeHideMouseAfterInactivity() => this.HideMouseAfterInactivity != null;
        public HideMouseAfterInactivity.Config HideMouseAfterInactivity = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class HideMouseAfterInactivity : Tweak {
        public override string Name => "Hide Mouse Cursor After Inactivity";
        public override string Description => "Hides the mouse cursor after a period of inactivity like video players do.";
        protected override string Author => "Anna";

        public class Config : TweakConfig {
            public float InactiveSeconds = 3.0f;
            public bool NoHideInCutscenes;
            public bool NoHideInCombat = true;
            public bool NoHideInInstance = true;
        }

        public Config TweakConfig { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool change) => {
            change |= ImGui.InputFloat("Hide after (seconds)", ref this.TweakConfig.InactiveSeconds, 0.1f);
            change |= ImGui.Checkbox("Don't hide in cutscenes", ref this.TweakConfig.NoHideInCutscenes);
            change |= ImGui.Checkbox("Don't hide in combat", ref this.TweakConfig.NoHideInCombat);
            change |= ImGui.Checkbox("Don't hide in instances", ref this.TweakConfig.NoHideInInstance);
        };

        private static class Signatures {
            internal const string SetCursorVisible = "E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 40 84 F6";
            internal const string StageOffset = "49 8D 9D ?? ?? ?? ?? 80 7B 0E 00 48 89 5D C0 C6 43 12 00 0F 84";
            internal const string MouseButtonHoldState = "8B 05 ?? ?? ?? ?? 48 89 5C 24 ?? 41 8B DF 38 1D";
        }

        private int? _stageOffset;
        private delegate*<IntPtr, byte, IntPtr> _setCursorVisible;
        private IntPtr _mouseButtonHoldState = IntPtr.Zero;

        private Vector2 _lastPosition = Vector2.Zero;
        private readonly Stopwatch _lastMoved = new();

        public override void Enable() {
            this.TweakConfig = this.LoadConfig<Config>() ?? this.PluginConfig.HideMouseAfterInactivity ?? new Config();

            if (this._stageOffset == null && Service.SigScanner.TryScanText(Signatures.StageOffset, out var offsetPtr)) {
                this._stageOffset = *(int*) (offsetPtr + 3);
            }

            if (this._setCursorVisible == null && Service.SigScanner.TryScanText(Signatures.SetCursorVisible, out var setVisible)) {
                this._setCursorVisible = (delegate*<IntPtr, byte, IntPtr>) setVisible;
            }

            if (this._mouseButtonHoldState == IntPtr.Zero) {
                Service.SigScanner.TryGetStaticAddressFromSig(Signatures.MouseButtonHoldState, out this._mouseButtonHoldState);
            }

            Service.PluginInterface.UiBuilder.Draw += this.GetInfo;
            Service.Framework.Update += this.HideMouse;

            base.Enable();
        }

        public override void Disable() {
            Service.Framework.Update -= this.HideMouse;
            Service.PluginInterface.UiBuilder.Draw -= this.GetInfo;

            base.Disable();
        }

        private void GetInfo() {
            var mouseDown = this._mouseButtonHoldState != IntPtr.Zero && *(byte*) this._mouseButtonHoldState > 0;
            if (ImGui.GetMousePos() != this._lastPosition || mouseDown) {
                this._lastMoved.Restart();
            }

            this._lastPosition = ImGui.GetMousePos();
        }

        private void HideMouse(Framework framework) {
            if (this._stageOffset == null) {
                return;
            }

            if (this.TweakConfig.NoHideInCutscenes) {
                var inCutscene = Service.Condition[ConditionFlag.WatchingCutscene]
                                 || Service.Condition[ConditionFlag.WatchingCutscene78];

                if (inCutscene) {
                    return;
                }
            }

            if (this.TweakConfig.NoHideInCombat && Service.Condition[ConditionFlag.InCombat]) {
                return;
            }

            if (this.TweakConfig.NoHideInInstance) {
                var inInstance = Service.Condition[ConditionFlag.BoundByDuty]
                                 || Service.Condition[ConditionFlag.BoundByDuty56]
                                 || Service.Condition[ConditionFlag.BoundByDuty95];

                if (inInstance) {
                    return;
                }
            }

            var atkStage = (IntPtr) AtkStage.GetSingleton();
            var offset = atkStage + this._stageOffset.Value;
            var isVisible = *(byte*) offset == 0;
            if (isVisible && this._lastMoved.Elapsed > TimeSpan.FromSeconds(this.TweakConfig.InactiveSeconds)) {
                this.SetCursorVisibility(false);
            }
        }

        private void SetCursorVisibility(bool visible) {
            if (this._stageOffset == null || this._setCursorVisible == null) {
                return;
            }

            var atkStage = (IntPtr) AtkStage.GetSingleton();
            if (atkStage == IntPtr.Zero) {
                return;
            }

            var offset = atkStage + this._stageOffset.Value;
            this._setCursorVisible(offset, (byte) (visible ? 1 : 0));
        }
    }
}
