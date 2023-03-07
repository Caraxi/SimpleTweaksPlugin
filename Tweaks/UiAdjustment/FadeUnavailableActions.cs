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
    }

    public Config TweakConfig { get; private set; } = null!;

    public override bool UseAutoConfig => true;
    
    public override void Setup()
    {
        if (Ready) return;
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
            if (node->AtkComponentIcon is not null && node->AtkComponentIcon->AtkComponentBase.OwnerNode is not null)
            {
                var iconComponentContainer = node->AtkComponentIcon->AtkComponentBase.OwnerNode;
                if (iconComponentContainer is not null)
                {
                    iconComponentContainer->AtkResNode.Color.A = (byte)(enable ? 0xFF : 0xFF * ((100 - TweakConfig.FadePercentage) / 100.0f));
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