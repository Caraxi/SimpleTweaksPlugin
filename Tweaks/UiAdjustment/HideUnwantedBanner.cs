#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

[TweakName("Hide Unwanted Banners")]
[TweakDescription("Hide information banners such as 'Venture Complete', or 'Levequest Accepted'.")]
[TweakAuthor("MidoriKami")]
[TweakAutoConfig]
[TweakReleaseVersion("1.8.3.0")]
public unsafe class HideUnwantedBanner : UiAdjustments.SubTweak {
    private delegate void ImageSetImageTextureDelegate(AtkUnitBase* addon, int bannerId, int a3, int sfxId);

    [TweakHook, Signature("48 89 5C 24 ?? 57 48 83 EC 30 48 8B D9 89 91", DetourName = nameof(OnSetImageTexture))]
    private readonly HookWrapper<ImageSetImageTextureDelegate>? setImageTextureHook = null!;

    private record BannerSetting(int Id, string Label);

    private readonly List<BannerSetting> banners = new() {
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
        new BannerSetting(121081, "Tribal Quest Accepted"),
        new BannerSetting(121082, "Tribal Quest Complete"),
    };
    
    public class Config : TweakConfig {
        public readonly List<int> HiddenBanners = new();
    }

    public Config TweakConfig { get; set; } = null!;
    
    protected void DrawConfig() {
        foreach (var banner in banners) {
            var enabled = TweakConfig.HiddenBanners.Contains(banner.Id);
            if (ImGui.Checkbox(banner.Label, ref enabled)) {
                if (TweakConfig.HiddenBanners.Contains(banner.Id) && !enabled) {
                    TweakConfig.HiddenBanners.Remove(banner.Id);
                    SaveConfig(TweakConfig);

                }
                else if(!TweakConfig.HiddenBanners.Contains(banner.Id) && enabled) {
                    TweakConfig.HiddenBanners.Add(banner.Id);
                    SaveConfig(TweakConfig);
                }
            }
        }
    }
    
    private void OnSetImageTexture(AtkUnitBase* addon, int bannerId, int a3, int soundEffectId) {
        var skipOriginal = false;
        
        try {
            skipOriginal = TweakConfig.HiddenBanners.Contains(bannerId);
        }
        catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in HideUnwantedBanners, let MidoriKami know!");
        }

        setImageTextureHook!.Original(addon, skipOriginal ? 0 : bannerId, a3, skipOriginal ? 0 : soundEffectId);
    }
}