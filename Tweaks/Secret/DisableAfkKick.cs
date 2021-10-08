using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace SimpleTweaksPlugin.Tweaks.Secret {
    public unsafe class DisableAfkKick : SecretTweaks.SubTweak {
        public override string Name => "Disable AFK Kick";

        public override void Enable() {
            External.Framework.Update += FrameworkOnUpdate;
            base.Enable();
        }

        private void FrameworkOnUpdate(Dalamud.Game.Framework framework) {
            if (External.Condition.Any()) {
                var atkModule = (byte*) Framework.Instance()->UIModule->GetRaptureAtkModule();
                *(float*)(atkModule + 0x276C8) = 0;
                *(float*)(atkModule + 0x276CC) = 0;
                *(float*)(atkModule + 0x276D0) = 0;
            }
        }

        public override void Disable() {
            External.Framework.Update -= FrameworkOnUpdate;
            base.Disable();
        }
    }
}
