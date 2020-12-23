using System;
using Dalamud.Hooking;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    public unsafe class NoFreeCompanyOnNamePlate : UiAdjustments.SubTweak {

        private IntPtr playerNamePlateSetTextAddress;
        private Hook<PlayerNamePlateSetText> playerNamePlateSetTextHook;
        private delegate IntPtr PlayerNamePlateSetText(byte* a1, byte a2, byte a3, byte* a4, byte* a5, byte* a6, uint a7);

        public override string Name => "Hide FC Name on Name Plate";

        public override void Setup() {
            try {
                playerNamePlateSetTextAddress = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 5C 24 ?? 45 38 A7 ?? ?? ?? ??");
                base.Setup();
            } catch (Exception ex) {
                SimpleLog.Log($"Failed Setup of {GetType().Name}: {ex.Message}");
            }
        }

        public override void Enable() {
            playerNamePlateSetTextHook ??= new Hook<PlayerNamePlateSetText>(playerNamePlateSetTextAddress, new PlayerNamePlateSetText(NamePlateDetour));
            playerNamePlateSetTextHook?.Enable();
            base.Enable();
        }

        public override void Disable() {
            playerNamePlateSetTextHook?.Disable();
            base.Disable();
        }

        public override void Dispose() {
            playerNamePlateSetTextHook?.Dispose();
            base.Dispose();
        }

        private IntPtr NamePlateDetour(byte* a1, byte a2, byte a3, byte* a4, byte* a5, byte* a6, uint a7) {
            a6[0] = 0;
            return playerNamePlateSetTextHook.Original(a1, a2, a3, a4, a5, a6, a7);
        }
    }
}
