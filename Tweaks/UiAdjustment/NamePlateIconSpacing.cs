using System;
using Dalamud;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public class NamePlateIconSpacing : UiAdjustments.SubTweak {
    public override string Name => "Name Plate Icon Spacing";
    public override string Description => "Increases the distance between status icons and character names on name plates.";
    
    public class Configs : TweakConfig {
        public byte Spacing = 5;
    }

    public Configs Config { get; private set; }

    private const byte MinSpacing = 0;
    private const byte MaxSpacing = 32;
    
    private IntPtr address = IntPtr.Zero;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        var spacing = (int)Config.Spacing;

        if (ImGui.SliderInt("Spacing", ref spacing, MinSpacing, MaxSpacing)) {
            if (spacing < MinSpacing) spacing = MinSpacing;
            if (spacing > MaxSpacing) spacing = MaxSpacing;
            Config.Spacing = (byte)spacing;
            hasChanged = true;
            SetSpacing(Config.Spacing);
        }
    };

    private void SetSpacing(byte spacing) {
        if (spacing < MinSpacing) spacing = MinSpacing;
        if (spacing > MaxSpacing) spacing = MaxSpacing;
        
        byte spacingByte = 0x06;

        unchecked {
            spacingByte -= spacing;
        }
        
        SafeMemory.WriteBytes(address, new byte[] { 0x8D, 0x53, spacingByte });
    }

    protected override void Enable() {
        address = Service.SigScanner.ScanText("8D 53 06 E8 ?? ?? ?? ?? 0F B6 54 24");
        if (address == IntPtr.Zero) throw new Exception("Failed to locate correct address.");

        Config = LoadConfig<Configs>() ?? new Configs();
        SetSpacing(Config.Spacing);
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        SetSpacing(0);
        base.Disable();
    }
}
