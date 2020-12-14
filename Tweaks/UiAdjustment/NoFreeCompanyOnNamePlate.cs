using System;
using Dalamud.Hooking;
using Dalamud.Plugin;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment {
    class NoFreeCompanyOnNamePlate : UiAdjustments.SubTweak {

        private IntPtr playerNamePlateSetTextAddress;
        private Hook<PlayerNamePlateSetText> playerNamePlateSetTextHook;
        private delegate IntPtr PlayerNamePlateSetText(IntPtr a1, byte a2, byte a3, string a4, string a5, string a6, uint a7);

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

        private IntPtr NamePlateDetour(IntPtr a1, byte a2, byte a3, string a4, string a5, string a6, uint a7) {
            return playerNamePlateSetTextHook.Original(a1, a2, a3, a4, a5, "", a7);
        }
    }
}
