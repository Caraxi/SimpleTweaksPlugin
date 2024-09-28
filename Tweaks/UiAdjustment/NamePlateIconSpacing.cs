using System;
using Dalamud;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Name Plate Icon Spacing")]
[TweakDescription("Increases the distance between status icons and character names on name plates.")]
[TweakAutoConfig]
public class NamePlateIconSpacing : UiAdjustments.SubTweak {
    public class Configs : TweakConfig {
        public byte Spacing = 5;
    }

    public Configs Config { get; private set; }

    private const byte MinSpacing = 0;
    private const byte MaxSpacing = 32;

    private nint address = nint.Zero;

    protected void DrawConfig(ref bool hasChanged) {
        var spacing = (int)Config.Spacing;

        if (ImGui.SliderInt("Spacing", ref spacing, MinSpacing, MaxSpacing)) {
            if (spacing < MinSpacing) spacing = MinSpacing;
            if (spacing > MaxSpacing) spacing = MaxSpacing;
            Config.Spacing = (byte)spacing;
            hasChanged = true;
            SetSpacing(Config.Spacing);
        }
    }

    private void SetSpacing(byte spacing) {
        if (spacing > MaxSpacing) spacing = MaxSpacing;

        byte spacingByte = 0x06;

        unchecked {
            spacingByte -= spacing;
        }

        SafeMemory.WriteBytes(address, [0x8D, 0x53, spacingByte]);
    }

    protected override void Enable() {
        address = Service.SigScanner.ScanText("8D 53 06 E8 ?? ?? ?? ?? 44 0F B6 44 24");
        if (address == nint.Zero) throw new Exception("Failed to locate correct address.");

        Config = LoadConfig<Configs>() ?? new Configs();
        SetSpacing(Config.Spacing);
    }

    protected override void Disable() {
        SetSpacing(0);
    }
}
