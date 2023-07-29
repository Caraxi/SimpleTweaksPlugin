using System.Runtime.InteropServices;
using Dalamud.Game;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public bool ShouldSerializeFixedShadowDistance() => FixedShadowDistance != null;
        public FixedShadowDistance.Configs FixedShadowDistance = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks {
    public unsafe class FixedShadowDistance : Tweak {
        public override string Name => "Fixed Shadow Distance";
        public override string Description => "Sets a fixed value for the shadow rendering, preventing it from changing when flying.";

        public override uint Version => 2;

        public class Configs : TweakConfig {
            public float ShadowDistance = 1800;
        }
        
        [StructLayout(LayoutKind.Explicit, Size = 0x3E0)]
        public struct ShadowManager {
            [FieldOffset(0x30)] public float BaseShadowDistance;
            [FieldOffset(0x34)] public float ShadowDistance;
            [FieldOffset(0x38)] public float ShitnessModifier;
            [FieldOffset(0x3C)] public float FlyingModifier;
        }

        private ShadowManager* shadowManager;
        public Configs Config { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.SliderFloat(LocString("Shadow Distance"), ref Config.ShadowDistance, 1, 1800, "%.0f");
        };

        public override void Setup() {
            shadowManager = *(ShadowManager**)Service.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8B 0C 02");
            if (shadowManager != null) base.Setup();
        }

        protected override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.FixedShadowDistance ?? new Configs();
            if (shadowManager == null) return;
            Service.Framework.Update += SetupShadows;
            base.Enable();
        }

        private void SetupShadows(Framework framework) {
            if (shadowManager == null) return;
            if (shadowManager->FlyingModifier > 1) shadowManager->FlyingModifier = 1;
            if (shadowManager->ShitnessModifier > 0.075f) shadowManager->ShitnessModifier = 0.075f;
            if (shadowManager->BaseShadowDistance != Config.ShadowDistance) {
                shadowManager->BaseShadowDistance = Config.ShadowDistance;
                shadowManager->ShadowDistance = Config.ShadowDistance;
            }
        }

        protected override void Disable() {
            SaveConfig(Config);
            PluginConfig.FixedShadowDistance = null;
            if (shadowManager != null) {
                shadowManager->FlyingModifier = 8;
                shadowManager->BaseShadowDistance = 225;
                shadowManager->ShitnessModifier = 0.5f;
            }
            Service.Framework.Update -= SetupShadows;
            base.Disable();
        }
    }
}
