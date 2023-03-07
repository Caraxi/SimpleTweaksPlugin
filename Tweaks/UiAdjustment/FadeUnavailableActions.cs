#nullable enable
using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class FadeUnavailableActions : UiAdjustments.SubTweak
{
    public override string Name => "Fade Unavailable Actions";
    public override string Description => "Instead of darkening icons, makes them transparent when unavailable";
    protected override string Author => "MidoriKami";

    private delegate nint DisableSlotDelegate(AtkComponentDragDrop* node, bool disable);

    [Signature("E8 ?? ?? ?? ?? 48 8B 17 B8", DetourName = nameof(OnDisableSlot))]
    private readonly Hook<DisableSlotDelegate>? onDisableSlotHook = null!;

    public class Config : TweakConfig
    {
        [TweakConfigOption("Fade Percentage", IntMax = 90, IntMin = 0, IntType = TweakConfigOptionAttribute.IntEditType.Slider, EditorSize = 150)]
        public int FadePercentage = 70;

        [TweakConfigOption("Apply Transparency to Frame")]
        public bool ApplyToFrame = true;
    }

    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
        AddChangelogNewTweak("1.8.3.1");
        AddChangelog(Changelog.UnreleasedVersion, "Tweak now only applies to the icon image itself and not the entire button");
        AddChangelog(Changelog.UnreleasedVersion, "Add option to apply transparency to the slot frame of the icon");
        
        SignatureHelper.Initialise(this);
        
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        
        onDisableSlotHook?.Enable();
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        
        onDisableSlotHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        onDisableSlotHook?.Dispose();
        base.Dispose();
    }

    private nint OnDisableSlot(AtkComponentDragDrop* node, bool enable)
    {
        var result = onDisableSlotHook!.Original(node, enable);

        try
        {
            if (Service.ClientState.LocalPlayer is { IsCasting: false })
            {
                if (node is not null && node->AtkComponentIcon is not null && node->AtkComponentIcon->IconImage is not null && node->AtkComponentIcon->Frame is not null)
                {
                    var conditionalTransparencyValue = (byte)(enable ? 0xFF : 0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
                    
                    var iconImage = node->AtkComponentIcon->IconImage;
                    var frameNode = node->AtkComponentIcon->Frame;
                    
                    iconImage->AtkResNode.Color.A = conditionalTransparencyValue;
                    frameNode->Color.A = TweakConfig.ApplyToFrame ? conditionalTransparencyValue : (byte) 0xFF;

                    // Force the game to un-darken the icons
                    if (!enable)
                    {
                        iconImage->AtkResNode.MultiplyRed = 100;
                        iconImage->AtkResNode.MultiplyGreen = 100;
                        iconImage->AtkResNode.MultiplyBlue = 100;
                        iconImage->AtkResNode.MultiplyRed_2 = 100;
                        iconImage->AtkResNode.MultiplyGreen_2 = 100;
                        iconImage->AtkResNode.MultiplyBlue_2 = 100;
                    }
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in FadeUnavailableActions, let MidoriKami know!");
        }

        return result;
    }
}