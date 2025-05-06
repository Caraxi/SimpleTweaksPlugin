#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
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
[Changelog("1.10.9.3", "Added banners for Stellar Missions", Author = "MidoriKami")]
[Changelog("1.10.9.3", "Added ability to add other banners after being seen")]
[Changelog("1.10.9.3", "Added image previews for banners")]
public unsafe class HideUnwantedBanner : UiAdjustments.SubTweak {
    private delegate void ImageSetImageTextureDelegate(AtkUnitBase* addon, int bannerId, int a3, int sfxId);

    [TweakHook, Signature("48 89 5C 24 ?? 57 48 83 EC 30 48 8B D9 89 91", DetourName = nameof(OnSetImageTexture))]
    private readonly HookWrapper<ImageSetImageTextureDelegate>? setImageTextureHook;

    private record BannerSetting(int Id, string Label, bool Custom = false);

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
        new BannerSetting(121561, "GATE Joined"),
        new BannerSetting(121562, "GATE Complete"),
        new BannerSetting(121563, "GATE Failed"),
        new BannerSetting(128370, "Stellar Mission Commenced"),
        new BannerSetting(128371, "Stellar Mission Abandoned"),
        new BannerSetting(128372, "Stellar Mission Failed"),
        new BannerSetting(128373, "Stellar Mission Complete"),
    };
    
    public class Config : TweakConfig {
        public readonly HashSet<int> HiddenBanners = [];
        public bool ShowPreview;
    }

    [TweakConfig] private Config TweakConfig { get; set; } = null!;
    private readonly HashSet<int> seenIds = [];
    
    protected void DrawConfig() {
        ImGui.Checkbox("Show Preview", ref TweakConfig.ShowPreview);
        ImGui.SameLine(ImGui.GetItemRectSize().X + 80);
        
        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);;
        if (ImGui.BeginCombo("##customBannerPicker", "Add Other Banner...")) {
            if (seenIds.Count == 0) {
                ImGui.Dummy(new Vector2(300, 1) * ImGuiHelpers.GlobalScale);
                using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))) {
                    ImGui.TextWrapped("Banners will be displayed here if they are detected. Come back when you see one you want to block.");
                }
            }

            foreach (var b in seenIds.ToArray()) {
                try {
                    var seenIcon = Service.TextureProvider.GetFromGameIcon(b).GetWrapOrDefault();
                    if (seenIcon != null) {
                        if (ImGui.ImageButton(seenIcon.ImGuiHandle, ImGuiHelpers.ScaledVector2(seenIcon.Width * 80f / seenIcon.Height, 80))) {
                            TweakConfig.HiddenBanners.Add(b);
                            SaveConfig(TweakConfig);
                            seenIds.Remove(b);
                        }
                    }
                } catch {
                    //
                }
            }
                
            ImGui.EndCombo();
        }
        
        if (ImGui.BeginTable("bannerList", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersOuter)) {
            foreach (var banner in banners.Concat(TweakConfig.HiddenBanners.Where(p => banners.All(b => b.Id != p)).Select(p => new BannerSetting(p, $"Custom Banner#{p}", true)))) {
                var enabled = TweakConfig.HiddenBanners.Contains(banner.Id);
                ImGui.TableNextColumn();
                if (TweakConfig.ShowPreview) {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 50 * ImGuiHelpers.GlobalScale - ImGui.GetTextLineHeightWithSpacing() / 2f);
                }
                
                if (ImGui.Checkbox($"##bannerToggle_{banner.Id}", ref enabled)) {
                    if (TweakConfig.HiddenBanners.Contains(banner.Id) && !enabled) {
                        TweakConfig.HiddenBanners.Remove(banner.Id);
                        SaveConfig(TweakConfig);
                    } else if (!TweakConfig.HiddenBanners.Contains(banner.Id) && enabled) {
                        TweakConfig.HiddenBanners.Add(banner.Id);
                        SaveConfig(TweakConfig);
                    }
                }
                
                ImGui.TableNextColumn();
                if (TweakConfig.ShowPreview) {
                    IDalamudTextureWrap? icon;
                    try {
                        icon = Service.TextureProvider.GetFromGameIcon(banner.Id).GetWrapOrDefault();
                    } catch {
                        icon = null;
                    }
                    
                    if (icon == null) {
                        ImGui.Text(banner.Label);
                    } else {
                        ImGui.Image(icon.ImGuiHandle, ImGuiHelpers.ScaledVector2(icon.Width * 100f / icon.Height, 100));
                    }
                } else {
                    ImGui.Text(banner.Label);
                }
            }
            
            ImGui.EndTable();
        }
    }

    private void OnSetImageTexture(AtkUnitBase* addon, int bannerId, int a3, int soundEffectId) {
        var skipOriginal = false;

        try {
            skipOriginal = TweakConfig.HiddenBanners.Contains(bannerId);
            if (!skipOriginal && banners.All(b => b.Id != bannerId)) seenIds.Add(bannerId);
        } catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in HideUnwantedBanners, let MidoriKami know!");
        }

        setImageTextureHook!.Original(addon, skipOriginal ? 0 : bannerId, a3, skipOriginal ? 0 : soundEffectId);
    }
}
