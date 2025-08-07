using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Bindings.ImGui;
using SimpleTweaksPlugin.ExtraPayloads;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Custom Timestamp Format")]
[TweakDescription("Customize the timestamps displayed on chat messages.")]
[TweakReleaseVersion("1.8.9.0")]
[TweakAutoConfig]
public unsafe class CustomTimestampFormat : ChatTweaks.SubTweak {
    public class Configs : TweakConfig {
        public string Format = "[HH:mm:ss]";
        public bool DoColor;
        public bool UseServerTime;
        public Vector3 Color = new(1);
    }

    private delegate byte* ApplyTextFormatDelegate(RaptureTextModule* raptureTextModule, uint addonTextId, int value);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 41 8D 55 0B", DetourName = nameof(FormatTextDetour))]
    private HookWrapper<ApplyTextFormatDelegate>? applyTextFormatHook;

    public Configs Config { get; private set; }

    protected void DrawConfig(ref bool hasChanged) {
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
    }

    private Utf8String* str;

    protected override void Enable() {
        if (str == null) str = Utf8String.FromString(string.Empty);
    }

    private byte* FormatTextDetour(RaptureTextModule* raptureTextModule, uint addonTextId, int value) {
        if (addonTextId is 7840 or 7841) {
            var time = DateTimeOffset.FromUnixTimeSeconds(value);

            if (str != null) {
                if (Config.DoColor) {
                    var seStr = new SeString();
                    seStr.Append(new ColorPayload(Config.Color));
                    seStr.Append((Config.UseServerTime ? time.DateTime : time.LocalDateTime).ToString(Config.Format));
                    seStr.Append(new ColorEndPayload());
                    var bytes = seStr.EncodeWithNullTerminator();
                    if (bytes.Length == 0 || bytes[0] == 0) {
                        str->SetString(string.Empty);
                    } else {
                        str->SetString(bytes);
                    }
                } else {
                    var text = (Config.UseServerTime ? time.DateTime : time.LocalDateTime).ToString(Config.Format);
                    str->SetString(string.IsNullOrEmpty(text) ? string.Empty : text);
                }

                return str->StringPtr;
            }
        }

        return applyTextFormatHook!.Original(raptureTextModule, addonTextId, value);
    }
}
