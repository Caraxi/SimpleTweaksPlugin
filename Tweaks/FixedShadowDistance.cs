using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Internal;
using ImGuiNET;
using SimpleTweaksPlugin.Helper;
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

        public class Configs : TweakConfig {
            public float ShadowDistance = 1800;
        }
        
        [StructLayout(LayoutKind.Explicit, Size = 0x3E0)]
        public struct ShadowManager {
            [FieldOffset(0x2C)] public float BaseShadowDistance;
            [FieldOffset(0x30)] public float ShadowDistance;
            [FieldOffset(0x34)] public float ShitnessModifier;
            [FieldOffset(0x38)] public float FlyingModifier;
        }

        private ShadowManager* shadowManager;
        public Configs Config { get; private set; }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            hasChanged |= ImGui.SliderFloat("Shadow Distance", ref Config.ShadowDistance, 1, 1800, "%.0f");
        };

        public override void Setup() {
            shadowManager = *(ShadowManager**)Common.Scanner.GetStaticAddressFromSig("89 50 28 48 8B 05 ?? ?? ?? ?? 8B 89 ?? ?? ?? ?? 89 48 2C C3 8B 4A 10");
            if (shadowManager != null) base.Setup();
        }

        public override void Enable() {
            Config = LoadConfig<Configs>() ?? PluginConfig.FixedShadowDistance ?? new Configs();
            if (shadowManager == null) return;
            External.Framework.Update += SetupShadows;
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
        
        public override void Disable() {
            SaveConfig(Config);
            PluginConfig.FixedShadowDistance = null;
            if (shadowManager != null) {
                shadowManager->FlyingModifier = 8;
                shadowManager->BaseShadowDistance = 225;
                shadowManager->ShitnessModifier = 0.5f;
            }
            External.Framework.Update -= SetupShadows;
            base.Disable();
        }
    }
}
