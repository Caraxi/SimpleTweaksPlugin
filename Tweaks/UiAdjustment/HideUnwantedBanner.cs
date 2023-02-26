#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public class HideUnwantedBanner : UiAdjustments.SubTweak
{
    public override string Name => "Hide Unwanted Banners";
    public override string Description => "Hide information banners such as 'Venture Complete', or 'Levequest Accepted'\nNote: Does not silence the accompanying sound notification.";
    protected override string Author => "MidoriKami";

    private delegate nint ImageSetImageTextureDelegate(nint addon, int a2, int a3, int a4);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 30 48 8B D9 89 91", DetourName = nameof(OnSetImageTexture))]
    private readonly Hook<ImageSetImageTextureDelegate>? setImageTextureHook = null!;

    private record BannerSetting(bool Enabled, int Id, string Label)
    {
        public bool Enabled { get; set; } = Enabled;
    }

    private class Config : TweakConfig
    {
        public readonly List<BannerSetting> Banners = new()
        {
            new BannerSetting(false, 120031, "Levequest Accepted"),
            new BannerSetting(false, 120032, "Levequest Complete"),
            new BannerSetting(false, 120055, "Delivery Complete"),
            new BannerSetting(false, 120081, "FATE Joined"),
            new BannerSetting(false, 120082, "FATE Complete"),
            new BannerSetting(false, 120083, "FATE Failed"),
            new BannerSetting(false, 120084, "FATE Joined EXP BONUS"),
            new BannerSetting(false, 120085, "FATE Complete EXP BONUS"),
            new BannerSetting(false, 120086, "FATE Failed EXP BONUS"),
            new BannerSetting(false, 120093, "Treasure Obtained!"),
            new BannerSetting(false, 120094, "Treasure Found!"),
            new BannerSetting(false, 120095, "Venture Commenced!"),
            new BannerSetting(false, 120096, "Venture Accomplished!"),
            new BannerSetting(false, 120141, "Voyage Commenced"),
            new BannerSetting(false, 120142, "Voyage Complete"),
        };
    }

    private Config TweakConfig { get; set; } = null!;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        foreach (var banner in TweakConfig.Banners)
        {
            var enabled = banner.Enabled;
            if (ImGui.Checkbox(banner.Label, ref enabled))
            {
                banner.Enabled = enabled;
                SaveConfig(TweakConfig);
            }
        }
    };
    
    public override void Setup()
    {
        if (Ready) return;
        
        SignatureHelper.Initialise(this);
        base.Setup();
    }

    public override void Enable()
    {
        TweakConfig = LoadConfig<Config>() ?? new Config();
        setImageTextureHook?.Enable();
        base.Enable();
    }

    public override void Disable()
    {
        SaveConfig(TweakConfig);
        setImageTextureHook?.Disable();
        base.Disable();
    }

    public override void Dispose()
    {
        setImageTextureHook?.Dispose();
        base.Dispose();
    }

    private nint OnSetImageTexture(nint addon, int a2, int a3, int a4)
    {
        var skipOriginal = false;
        
        try
        {
            if (TweakConfig.Banners.Any(bannerSetting => bannerSetting.Enabled && bannerSetting.Id == a2))
            {
                skipOriginal = true;
            }
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in HideUnwantedBanners, let MidoriKami know!");
        }

        return skipOriginal ? nint.Zero : setImageTextureHook!.Original(addon, a2, a3, a4);
    }
}