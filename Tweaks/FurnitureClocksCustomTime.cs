using System;
using System.ComponentModel;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks;

[TweakName("Use Custom Time for Furniture Clocks")]
[TweakDescription("Changes the time displayed on chronometer furniture.")]
[TweakAutoConfig]
[TweakReleaseVersion("1.9.2.0")]
public unsafe class FurnitureClocksCustomTime : Tweak {
    public enum TimeMode {
        [Description("Local Time")] Local,
        [Description("Server Time")] Server,
        [Description("Custom Timezone")] Custom,
        [Description("Fixed Time")] Fixed,
    }
    
    public class Configs : TweakConfig {
        [TweakConfigOption("Mode", EditorSize = 200)] public TimeMode Mode = TimeMode.Local;
        public int CustomTimeOffset;
        public int FixedTime;
    }

    public Configs Config { get; set; } = null!;
    
    private void DrawConfig() {
        if (Config.Mode == TimeMode.Fixed) {
            var h = Config.FixedTime / 60;
            var m = Config.FixedTime % 60;

            ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
            var modified = ImGui.InputInt("##Hour", ref h);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
            modified |= ImGui.InputInt("Time", ref m);

            if (modified) {
                while (h <= 0) h += 12;
                while (h > 12) h -= 12;
                Config.FixedTime = h * 60 + m;
            }

        } else if (Config.Mode == TimeMode.Custom) {
            ImGui.InputInt("Offset (minutes)", ref Config.CustomTimeOffset, 15);
        } else {
            var o = $"{(Config.Mode == TimeMode.Local ? DateTimeOffset.Now.Offset.TotalMinutes : 0f)}";
            ImGui.InputText("Offset (minutes)", ref o, 15, ImGuiInputTextFlags.ReadOnly);
        }
    }
    
    private delegate void* UpdateClockTime(void* a1);

    [TweakHook, Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 8B F1", DetourName = nameof(UpdateTimeDetour))]
    private readonly HookWrapper<UpdateClockTime> updateTimeHook = null!;

    private float GetTime() {
        return (float) (Config.Mode switch {
            TimeMode.Local => DateTime.Now.TimeOfDay.TotalSeconds,
            TimeMode.Server => DateTime.UtcNow.TimeOfDay.TotalSeconds,
            TimeMode.Custom => DateTime.UtcNow.TimeOfDay.TotalSeconds + 60 * Config.CustomTimeOffset,
            TimeMode.Fixed => 60 * Config.FixedTime,
            _ => DateTime.Now.TimeOfDay.TotalSeconds,
        });
    }

    private void* UpdateTimeDetour(void* a1) {
        var envManager = EnvManager.Instance();
        if (envManager == null) return updateTimeHook!.Original(a1);
        
        var resetTime = envManager->DayTimeSeconds;
        envManager->DayTimeSeconds = GetTime();
        try {
            return updateTimeHook!.Original(a1);
        } finally {
            envManager->DayTimeSeconds = resetTime;
        }
    }
}

