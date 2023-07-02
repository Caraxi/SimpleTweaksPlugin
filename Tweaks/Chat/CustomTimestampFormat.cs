/*using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.ExtraPayloads;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

public unsafe class CustomTimestampFormat : ChatTweaks.SubTweak {
    public override string Name => "Custom Timestamp Format";
    public override string Description => "Customize the timestamps displayed on chat messages.";

    public class Configs : TweakConfig {
        public string Format = "[HH:mm:ss]";
        public bool DoColor;
        public bool UseServerTime;
        public Vector3 Color = new(1);
    }

    private delegate byte* ApplyTextFormatDelegate(RaptureTextModule* raptureTextModule, uint addonTextId, int value);
    private HookWrapper<ApplyTextFormatDelegate>? applyTextFormatHook;
    
    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox("Use Server Time", ref Config.UseServerTime);
        hasChanged |= ImGui.Checkbox("Apply Color", ref Config.DoColor);
        if (Config.DoColor) {
            ImGui.SameLine();
            ImGui.ColorEdit3("##color", ref Config.Color, ImGuiColorEditFlags.NoInputs);
        }
        
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.InputText("Format##timestampFormatEditInput", ref Config.Format, 80);
        ImGui.SameLine();
        ImGui.TextDisabled($"   {(Config.UseServerTime ? DateTime.UtcNow : DateTime.Now).ToString(Config.Format)}");

        ImGui.Text("Presets:");
        if (ImGui.BeginTable("presetList", (int)(ImGui.GetContentRegionAvail().X / 150) + 1)) {
            void PresetButton(string format) {
                ImGui.TableNextColumn();
                
                if (ImGui.Button($"{(Config.UseServerTime ? DateTime.UtcNow : DateTime.Now).ToString(format)}", new Vector2(ImGui.GetContentRegionAvail().X, 25 * ImGuiHelpers.GlobalScale))) {
                    Config.Format = format;
                }
            }
            
            PresetButton("[HH:mm]");
            PresetButton("[HH:mm:ss]");
            PresetButton("[hh:mm tt]");
            PresetButton("[hh:mm:ss tt]");
            ImGui.EndTable();
        }
    };

    public override void Setup() {
        AddChangelogNewTweak(Changelog.UnreleasedVersion);
        base.Setup();
    }

    public override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        applyTextFormatHook ??= Common.Hook<ApplyTextFormatDelegate>("E8 ?? ?? ?? ?? 41 B0 07", FormatTextDetour);
        applyTextFormatHook?.Enable();
        
        base.Enable();
    }

    private byte* FormatTextDetour(RaptureTextModule* raptureTextModule, uint addonTextId, int value) {
        if (addonTextId is 7840 or 7841) {
            var time =  DateTimeOffset.FromUnixTimeSeconds(value);
            var str = (Utf8String*) (raptureTextModule + 0x9C0);
            
            if (Config.DoColor) {
                var seStr = new SeString();
                seStr.Append(new ColorPayload(Config.Color));
                seStr.Append((Config.UseServerTime ? time.DateTime : time.LocalDateTime).ToString(Config.Format));
                seStr.Append(new ColorEndPayload());
                str->SetString(seStr.Encode());
            } else {
                str->SetString((Config.UseServerTime ? time.DateTime : time.LocalDateTime).ToString(Config.Format));
            }
            
            
            return str->StringPtr;
        }

        return applyTextFormatHook!.Original(raptureTextModule, addonTextId, value);
    }

    public override void Disable() {
        applyTextFormatHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose() {
        applyTextFormatHook?.Dispose();
        base.Dispose();
    }
}
*/
