using System;
using Dalamud;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks; 

public class LegacyCameraLock : Tweak {
    public override string Name => "Legacy Camera Lock";
    public override string Description => "Prevents camera rotation when using Legacy movement type.";
    
    private IntPtr changeAddress = IntPtr.Zero;
    private byte[] originalBytes = new byte[6];

    private bool changesEnabled;
    
    public class Configs : TweakConfig {
        [TweakConfigOption("Disable While Auto Running")]
        public bool DisableWhileAutoRunning = false;
    }

    public Configs Config { get; private set; }

    public override bool UseAutoConfig => true;

    protected override void ConfigChanged() {
        base.ConfigChanged();
        if (Enabled) {
            Common.FrameworkUpdate -= OnFrameworkUpdate;
            if (Config.DisableWhileAutoRunning) {
                Common.FrameworkUpdate += OnFrameworkUpdate;
            } else {
                EnableChanges();
            }
        }
        
    }

    private void OnFrameworkUpdate() {

        var isAutoRunning = InputManager.IsAutoRunning();

        if (changesEnabled == InputManager.IsAutoRunning()) {
            if (changesEnabled) {
                DisableChanges();
            } else {
                EnableChanges();
            }
        }
    }

    public override void Enable() {
        if (Enabled) return;
        Config = LoadConfig<Configs>() ?? new Configs();
        try {
            if (changeAddress == IntPtr.Zero) {
                changeAddress = Common.Scanner.ScanText("0F 86 ?? ?? ?? ?? 48 8B 4D 77");
                SimpleLog.Verbose($"Found Signature: {changeAddress.ToInt64():X}");
            }
        } catch {
            SimpleLog.Error("Failed to find Signature");
            return;
        }
        EnableChanges();
        base.Enable();
        ConfigChanged();
    }

    private void EnableChanges() {
        if (changesEnabled) return;
        if (changeAddress == IntPtr.Zero) return;
            
        originalBytes = new byte[6];
        var readOriginalSuccess = SafeMemory.ReadBytes(changeAddress, 6, out originalBytes);
        if (readOriginalSuccess) {
            var relAddr = BitConverter.ToInt32(originalBytes, 2) + 1;
            var relAddrBytes = BitConverter.GetBytes(relAddr);
            var writeNewSuccess = SafeMemory.WriteBytes(changeAddress, new byte[6] {0xE9, relAddrBytes[0], relAddrBytes[1], relAddrBytes[2], relAddrBytes[3], 0x90});
            if (writeNewSuccess) {
                changesEnabled = true;
            } else {
                SimpleLog.Error("Failed to write new instruction");
            }
        } else {
            originalBytes = new byte[0];
            SimpleLog.Error("Failed to read original instruction");
        }
    }
    
    public void DisableChanges() {
        if (!changesEnabled) return;
        if (changeAddress == IntPtr.Zero) return;
            
        var writeOriginalSuccess = SafeMemory.WriteBytes(changeAddress, originalBytes);
        if (!writeOriginalSuccess) {
            SimpleLog.Error("Failed to write original instruction");
        } else {
            changesEnabled = false;
        }
    }
    
    public override void Disable() {
        if (!Enabled) return;
        Common.FrameworkUpdate -= OnFrameworkUpdate;
        DisableChanges();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        if (Enabled) Disable();
    }
}