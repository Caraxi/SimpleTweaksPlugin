using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks
{
    internal class InterruptCastOnDeath : Tweak
    {
        public override string Name => "Interrupt Cast on Target Death";
        public override string Description => "Fixes incorrect behaviour where the cast does not end on target death.";

        private delegate void CastCancel();
        [Signature("48 83 EC 38 33 D2 C7 44 24 ?? ?? ?? ?? ?? 45 33 C9")]
        private CastCancel? CastCanc { get; init; }

        bool Cast { get; set; }

        public override void Enable()
        {
            SignatureHelper.Initialise(this);
            Service.Framework.Update += Framework_Update;
            Service.Condition.ConditionChange += Condition_ConditionChange;
        }

        private void Condition_ConditionChange(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Casting)
            {
                Cast = value;
            }
        }

        private void Framework_Update(Dalamud.Game.Framework framework)
        {
            if (Cast)
            {
                if (( (BattleChara) Service.Objects.SearchById(Service.ClientState.LocalPlayer.CastTargetObjectId)).CurrentHp == 0)
                {
                    CastCanc();
                }
            }
        }

        public override void Disable()
        {
            if (!Enabled) return;
            Service.Framework.Update -= Framework_Update;
            Service.Condition.ConditionChange -= Condition_ConditionChange;
        }

        public override void Dispose()
        {
            Disable();
            base.Dispose();
        }
    }
}
