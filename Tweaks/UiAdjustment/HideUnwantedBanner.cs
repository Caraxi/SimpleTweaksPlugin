﻿#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public class HideUnwantedBanner : UiAdjustments.SubTweak
{
    public override string Name => "Hide Unwanted Banners";
    public override string Description => "Hide information banners such as 'Venture Complete', or 'Levequest Accepted'";
    protected override string Author => "MidoriKami";

    private delegate void ImageSetImageTextureDelegate(nint addon, int a2, int a3, int a4);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 30 48 8B D9 89 91", DetourName = nameof(OnSetImageTexture))]
    private readonly Hook<ImageSetImageTextureDelegate>? setImageTextureHook = null!;

    private record BannerSetting(int Id, string Label);

    private readonly List<BannerSetting> banners = new()
    {
        new BannerSetting(120031, "Levequest Accepted"),
        new BannerSetting(120032, "Levequest Complete"),
        new BannerSetting(120055, "Delivery Complete"),
        new BannerSetting(120081, "FATE Joined"),
        new BannerSetting(120082, "FATE Complete"),
        new BannerSetting(120083, "FATE Failed"),
        new BannerSetting(120084, "FATE Joined EXP BONUS"),
        new BannerSetting(120085, "FATE Complete EXP BONUS"),
        new BannerSetting(120086, "FATE Failed EXP BONUS"),
        new BannerSetting(120093, "Treasure Obtained!"),
        new BannerSetting(120094, "Treasure Found!"),
        new BannerSetting(120095, "Venture Commenced!"),
        new BannerSetting(120096, "Venture Accomplished!"),
        new BannerSetting(120141, "Voyage Commenced"),
        new BannerSetting(120142, "Voyage Complete"),
    };
    
    private class Config : TweakConfig
    {
        public readonly List<int> HiddenBanners = new();
    }

    private Config TweakConfig { get; set; } = null!;
    
    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        foreach (var banner in banners)
        {
            var enabled = TweakConfig.HiddenBanners.Contains(banner.Id);
            if (ImGui.Checkbox(banner.Label, ref enabled))
            {
                if (TweakConfig.HiddenBanners.Contains(banner.Id) && !enabled)
                {
                    TweakConfig.HiddenBanners.Remove(banner.Id);
                    SaveConfig(TweakConfig);
                }
                else if(!TweakConfig.HiddenBanners.Contains(banner.Id) && enabled)
                {
                    TweakConfig.HiddenBanners.Add(banner.Id);
                    SaveConfig(TweakConfig);
                }
            }
        }
    };
    
    public override void Setup()
    {
        AddChangelogNewTweak(Changelog.UnreleasedVersion).Author("MidoriKami");
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

    private void OnSetImageTexture(nint addon, int a2, int a3, int a4)
    {
        var skipOriginal = false;
        
        try
        {
            skipOriginal = TweakConfig.HiddenBanners.Contains(a2);
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Something went wrong in HideUnwantedBanners, let MidoriKami know!");
        }

        if(!skipOriginal) setImageTextureHook!.Original(addon, a2, a3, a4);
    }
}