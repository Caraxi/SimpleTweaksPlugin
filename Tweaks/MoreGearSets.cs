using System;
using Dalamud;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks; 

public class MoreGearSets : Tweak {
    public override string Name => "More Gear Sets";
    public override string Description => "Increases maximum gear sets to 100.";
    protected override string Author => "UnknownX";

    private const byte numGearSets = 100; // Cannot be increased above 100
    private IntPtr changeAddress = IntPtr.Zero;
    private byte[] originalBytes = new byte[7];

    public override void Enable() {
        if (Enabled) return;
        changeAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 0F B6 F0 41 8B ED");
        if (SafeMemory.ReadBytes(changeAddress, 7, out originalBytes)) {
            if (SafeMemory.WriteBytes(changeAddress, new byte[] {0xB8, numGearSets, 0x00, 0x00, 0x00, 0x90, 0x90})) {
                base.Enable();
            } else {
                SimpleLog.Error("Failed to write new instruction");
            }
        } else {
            SimpleLog.Error("Failed to read original instruction");
        }
    }

    public override void Disable() {
        if (!Enabled) return;
        if (!SafeMemory.WriteBytes(changeAddress, originalBytes)) {
            SimpleLog.Error("Failed to write original instruction");
        }
        base.Disable();
    }

    public override void Dispose() {
        Disable();
        base.Dispose();
    }
}