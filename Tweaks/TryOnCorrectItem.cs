using Dalamud.Game.ClientState.Keys;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class TryOnCorrectItem : Tweak {
    public override string Name => "Try On Correct Item";
    public override string Description => "Show the correct item when trying on a glamoured item.";

    private delegate byte TryOn(uint unknownCanEquip, uint itemBaseId, ulong stainColor, uint itemGlamourId, byte unknownByte);
    private HookWrapper<TryOn> tryOnHook;

    public class Configs : TweakConfig {
        [TweakConfigOption("Hold Shift to try on unglamoured item.")]
        public bool ShiftForUnglamoured = true;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        tryOnHook ??= Common.Hook<TryOn>("E8 ?? ?? ?? ?? EB 35 BA", TryOnDetour);
        tryOnHook?.Enable();
        base.Enable();
    }

    private byte TryOnDetour(uint unknownCanEquip, uint itemBaseId, ulong stainColor, uint itemGlamourId, byte unknownByte) {
        if (Config.ShiftForUnglamoured && Service.KeyState[VirtualKey.SHIFT]) return tryOnHook.Original(unknownCanEquip, itemBaseId, stainColor, 0, unknownByte);
        return tryOnHook.Original(unknownCanEquip, itemGlamourId != 0 ? itemGlamourId : itemBaseId, stainColor, 0, unknownByte);
    }

    public override void Disable() {
        SaveConfig(Config);
        tryOnHook?.Disable();
        base.Disable();
    }

    public override void Dispose() {
        tryOnHook?.Dispose();
        base.Dispose();
    }
}